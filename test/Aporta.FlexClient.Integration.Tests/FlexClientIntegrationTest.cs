using System.Diagnostics;
using System.Net;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using NUnit.Framework;
using Z9Flex;
using Z9Flex.Client.Models;
using FlexApiClient = Z9Flex.Client.FlexClient;

namespace Aporta.FlexClient.Integration.Tests;

[TestFixture]
public class FlexClientIntegrationTest
{
    private const string BaseUrl = "https://localhost:5001";
    private const int AportaStartupTimeoutSeconds = 60;

    private static readonly string AportaPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "git", "Aporta");

    private Process? _aportaProcess;

    private static string FindDotnet()
    {
        var candidates = new[]
        {
            "/opt/homebrew/opt/dotnet@8/bin/dotnet",
            "/usr/local/share/dotnet/dotnet",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
            "dotnet"
        };
        return candidates.FirstOrDefault(File.Exists) ?? "dotnet";
    }

    private static HttpClientHandler CreateHandler()
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
    }

    private static FlexApiClient CreateUnauthenticatedClient(HttpMessageHandler handler)
    {
        var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: new HttpClient(handler))
        {
            BaseUrl = BaseUrl
        };
        return new FlexApiClient(adapter);
    }

    private static FlexApiClient CreateAuthenticatedClient(Z9AuthenticationProvider authProvider)
    {
        var adapter = new HttpClientRequestAdapter(authProvider,
            httpClient: new HttpClient(CreateHandler()))
        {
            BaseUrl = BaseUrl
        };
        return new FlexApiClient(adapter);
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var dotnetPath = FindDotnet();
        var aportaProjectPath = Path.Combine(AportaPath, "src", "Aporta");

        Assert.That(Directory.Exists(aportaProjectPath), Is.True,
            $"Aporta project not found at {aportaProjectPath}");

        var psi = new ProcessStartInfo(dotnetPath, $"run --project \"{aportaProjectPath}\" -- --cleanDatabase true")
        {
            WorkingDirectory = AportaPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _aportaProcess = Process.Start(psi);
        Assert.That(_aportaProcess, Is.Not.Null, "Failed to start Aporta process");

        // Read output on background threads so the process doesn't block
        _aportaProcess!.BeginOutputReadLine();
        _aportaProcess.BeginErrorReadLine();
        _aportaProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) TestContext.Progress.WriteLine($"[APORTA] {e.Data}");
        };
        _aportaProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) TestContext.Progress.WriteLine($"[APORTA-ERR] {e.Data}");
        };

        // Poll until Aporta responds
        using var pollHandler = CreateHandler();
        using var pollClient = new HttpClient(pollHandler) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(AportaStartupTimeoutSeconds);
        var ready = false;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await pollClient.GetAsync($"{BaseUrl}/door/list");
                // 401 means Aporta is up (just not authenticated)
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.OK)
                {
                    ready = true;
                    break;
                }
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(1000);
        }

        Assert.That(ready, Is.True, "Aporta did not become ready within the timeout");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_aportaProcess is { HasExited: false })
        {
            _aportaProcess.Kill(entireProcessTree: true);
            _aportaProcess.WaitForExit(5000);
        }

        _aportaProcess?.Dispose();
    }

    [Test]
    public async Task FlexClientIntegration()
    {
        // ================================================================
        // Test 1: 401 without token
        // ================================================================
        TestContext.Progress.WriteLine("Test 1: Verify 401 without session token...");
        using var unauthHandler = CreateHandler();
        var unauthClient = CreateUnauthenticatedClient(unauthHandler);
        var ex401 = Assert.ThrowsAsync<ApiException>(async () =>
            await unauthClient.Dev.List.GetAsync());
        Assert.That(ex401!.ResponseStatusCode, Is.EqualTo(401));
        TestContext.Progress.WriteLine("Got 401 as expected");

        // ================================================================
        // Test 2: Authenticate with admin/pass
        // ================================================================
        TestContext.Progress.WriteLine("Test 2: Authenticating with admin/pass...");
        var authProvider = Z9AuthenticationProvider.CreateInstance(
            BaseUrl, () => ("admin", "pass"), CreateHandler());
        await authProvider.RefreshTokenAsync();
        Assert.That(authProvider.CurrentAuthenticationResult.Authenticated, Is.True);
        Assert.That(authProvider.CurrentAuthenticationResult.SessionToken, Is.Not.Null.And.Not.Empty);
        TestContext.Progress.WriteLine(
            $"Authenticated, token={authProvider.CurrentAuthenticationResult.SessionToken![..8]}...");

        // ================================================================
        // Test 3: Bad credentials
        // ================================================================
        TestContext.Progress.WriteLine("Test 3: Authenticating with bad credentials...");
        var badAuthProvider = Z9AuthenticationProvider.CreateInstance(
            BaseUrl, () => ("admin", "wrong"), CreateHandler());
        await badAuthProvider.RefreshTokenAsync();
        Assert.That(badAuthProvider.CurrentAuthenticationResult.Authenticated, Is.False);
        TestContext.Progress.WriteLine("Bad credentials correctly rejected");

        // ================================================================
        // Test 4: List endpoints — empty database, verify deserialization
        // ================================================================
        TestContext.Progress.WriteLine("Test 4: Testing list endpoints...");
        var client = CreateAuthenticatedClient(authProvider);

        // Door list
        var doorList = await client.Door.List.GetAsync();
        Assert.That(doorList, Is.Not.Null);
        Assert.That(doorList!.InstanceList, Is.Not.Null);
        Assert.That(doorList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  door/list: count={doorList.Count}");

        // Dev list
        var devList = await client.Dev.List.GetAsync();
        Assert.That(devList, Is.Not.Null);
        Assert.That(devList!.InstanceList, Is.Not.Null);
        Assert.That(devList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  dev/list: count={devList.Count}");

        // Sensor list
        var sensorList = await client.Sensor.List.GetAsync();
        Assert.That(sensorList, Is.Not.Null);
        Assert.That(sensorList!.InstanceList, Is.Not.Null);
        Assert.That(sensorList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  sensor/list: count={sensorList.Count}");

        // Actuator list
        var actuatorList = await client.Actuator.List.GetAsync();
        Assert.That(actuatorList, Is.Not.Null);
        Assert.That(actuatorList!.InstanceList, Is.Not.Null);
        Assert.That(actuatorList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  actuator/list: count={actuatorList.Count}");

        // CredReader list
        var credReaderList = await client.CredReader.List.GetAsync();
        Assert.That(credReaderList, Is.Not.Null);
        Assert.That(credReaderList!.InstanceList, Is.Not.Null);
        Assert.That(credReaderList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  credReader/list: count={credReaderList.Count}");

        // Cred list
        var credList = await client.Cred.List.GetAsync();
        Assert.That(credList, Is.Not.Null);
        Assert.That(credList!.InstanceList, Is.Not.Null);
        Assert.That(credList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  cred/list: count={credList.Count}");

        // CredTemplate list
        var credTemplateList = await client.CredTemplate.List.GetAsync();
        Assert.That(credTemplateList, Is.Not.Null);
        Assert.That(credTemplateList!.InstanceList, Is.Not.Null);
        Assert.That(credTemplateList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  credTemplate/list: count={credTemplateList.Count}");

        // Sched list
        var schedList = await client.Sched.List.GetAsync();
        Assert.That(schedList, Is.Not.Null);
        Assert.That(schedList!.InstanceList, Is.Not.Null);
        Assert.That(schedList.Count, Is.GreaterThanOrEqualTo(0));
        var initialSchedCount = schedList.Count;
        TestContext.Progress.WriteLine($"  sched/list: count={schedList.Count}");

        // DoorAccessPriv list
        var privList = await client.DoorAccessPriv.List.GetAsync();
        Assert.That(privList, Is.Not.Null);
        Assert.That(privList!.InstanceList, Is.Not.Null);
        Assert.That(privList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  doorAccessPriv/list: count={privList.Count}");

        // Evt list
        var evtList = await client.Evt.List.GetAsync();
        Assert.That(evtList, Is.Not.Null);
        Assert.That(evtList!.InstanceList, Is.Not.Null);
        Assert.That(evtList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  evt/list: count={evtList.Count}");

        // Hol list
        var holList = await client.Hol.List.GetAsync();
        Assert.That(holList, Is.Not.Null);
        Assert.That(holList!.InstanceList, Is.Not.Null);
        Assert.That(holList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  hol/list: count={holList.Count}");

        // HolCal list
        var holCalList = await client.HolCal.List.GetAsync();
        Assert.That(holCalList, Is.Not.Null);
        Assert.That(holCalList!.InstanceList, Is.Not.Null);
        Assert.That(holCalList.Count, Is.GreaterThanOrEqualTo(0));
        TestContext.Progress.WriteLine($"  holCal/list: count={holCalList.Count}");

        // HolType list
        var holTypeList = await client.HolType.List.GetAsync();
        Assert.That(holTypeList, Is.Not.Null);
        Assert.That(holTypeList!.InstanceList, Is.Not.Null);
        Assert.That(holTypeList.Count, Is.GreaterThanOrEqualTo(0));
        var initialHolTypeCount = holTypeList.Count;
        TestContext.Progress.WriteLine($"  holType/list: count={holTypeList.Count}");

        // ================================================================
        // Test 5: CRUD — schedule
        // ================================================================
        TestContext.Progress.WriteLine("Test 5: CRUD schedule...");

        // Save
        var newSched = new Z9Flex.Client.Models.Sched
        {
            Unid = 999,
            Name = "Flex Test Schedule",
            Tag = "flex-test",
            Elements = new List<SchedElement>
            {
                new()
                {
                    SchedDays = new List<int?> { 1, 2, 3, 4, 5 },
                    Start = new Time(8, 0, 0),
                    Stop = new Time(17, 0, 0)
                }
            }
        };
        var saveResult = await client.Sched.Save.PostAsSavePostResponseAsync(newSched);
        Assert.That(saveResult, Is.Not.Null);
        Assert.That(saveResult!.Instance, Is.Not.Null);
        Assert.That(saveResult.Instance!.Name, Is.EqualTo("Flex Test Schedule"));
        TestContext.Progress.WriteLine($"  Saved schedule: unid={saveResult.Instance.Unid}");

        // Verify count increased
        var schedListAfterSave = await client.Sched.List.GetAsync();
        Assert.That(schedListAfterSave!.Count, Is.GreaterThan(initialSchedCount!.Value));

        // Update
        var updatedSched = new Z9Flex.Client.Models.Sched
        {
            Unid = 999,
            Name = "Updated Flex Schedule",
            Tag = "flex-test-updated",
            Elements = new List<SchedElement>
            {
                new()
                {
                    SchedDays = new List<int?> { 1, 2, 3, 4, 5, 6 },
                    Start = new Time(7, 0, 0),
                    Stop = new Time(19, 0, 0)
                }
            }
        };
        var updateResult = await client.Sched.Update["999"].PostAsUpdatePostResponseAsync(updatedSched);
        Assert.That(updateResult, Is.Not.Null);
        Assert.That(updateResult!.Instance, Is.Not.Null);
        Assert.That(updateResult.Instance!.Name, Is.EqualTo("Updated Flex Schedule"));
        TestContext.Progress.WriteLine("  Updated schedule name");

        // Delete
        await client.Sched.DeletePath["999"].PostAsync();
        TestContext.Progress.WriteLine("  Deleted schedule");

        // Verify count restored
        var schedListAfterDelete = await client.Sched.List.GetAsync();
        Assert.That(schedListAfterDelete!.Count, Is.EqualTo(initialSchedCount));
        TestContext.Progress.WriteLine("  Schedule count restored after delete");

        // ================================================================
        // Test 6: CRUD — holiday type
        // ================================================================
        TestContext.Progress.WriteLine("Test 6: CRUD holiday type...");

        // Save
        var newHolType = new Z9Flex.Client.Models.HolType
        {
            Unid = 500,
            Name = "Flex Test Holiday",
            Tag = "flex-hol"
        };
        var htSaveResult = await client.HolType.Save.PostAsSavePostResponseAsync(newHolType);
        Assert.That(htSaveResult, Is.Not.Null);
        Assert.That(htSaveResult!.Instance, Is.Not.Null);
        Assert.That(htSaveResult.Instance!.Name, Is.EqualTo("Flex Test Holiday"));
        TestContext.Progress.WriteLine("  Saved holiday type");

        // Update
        var updatedHolType = new Z9Flex.Client.Models.HolType
        {
            Unid = 500,
            Name = "Updated Holiday",
            Tag = "flex-hol-updated"
        };
        var htUpdateResult = await client.HolType.Update["500"].PostAsUpdatePostResponseAsync(updatedHolType);
        Assert.That(htUpdateResult, Is.Not.Null);
        Assert.That(htUpdateResult!.Instance, Is.Not.Null);
        Assert.That(htUpdateResult.Instance!.Name, Is.EqualTo("Updated Holiday"));
        TestContext.Progress.WriteLine("  Updated holiday type");

        // Delete
        await client.HolType.DeletePath["500"].PostAsync();
        TestContext.Progress.WriteLine("  Deleted holiday type");

        // Verify count restored
        var holTypeListAfterDelete = await client.HolType.List.GetAsync();
        Assert.That(holTypeListAfterDelete!.Count, Is.EqualTo(initialHolTypeCount));
        TestContext.Progress.WriteLine("  Holiday type count restored after delete");

        // ================================================================
        // Test 7: Pagination
        // ================================================================
        TestContext.Progress.WriteLine("Test 7: Pagination...");
        var paginatedCreds = await client.Cred.List.GetAsync(config =>
        {
            config.QueryParameters.Max = 1;
            config.QueryParameters.Offset = 0;
        });
        Assert.That(paginatedCreds, Is.Not.Null);
        Assert.That(paginatedCreds!.Max, Is.EqualTo(1));
        Assert.That(paginatedCreds.Offset, Is.EqualTo(0));
        Assert.That(paginatedCreds.InstanceList!.Count, Is.LessThanOrEqualTo(1));
        TestContext.Progress.WriteLine(
            $"  Pagination: max={paginatedCreds.Max}, offset={paginatedCreds.Offset}, returned={paginatedCreds.InstanceList.Count}");

        // ================================================================
        // Test 8: Terminate session
        // ================================================================
        TestContext.Progress.WriteLine("Test 8: Terminating session...");
        await client.Terminate.GetAsync();
        TestContext.Progress.WriteLine("  Session terminated");

        // Verify next call returns 401
        var exAfterTerminate = Assert.ThrowsAsync<ApiException>(async () =>
            await client.Cred.List.GetAsync());
        Assert.That(exAfterTerminate!.ResponseStatusCode, Is.EqualTo(401));
        TestContext.Progress.WriteLine("  Confirmed 401 after termination");

        TestContext.Progress.WriteLine("All FlexClient integration tests passed!");
    }
}
