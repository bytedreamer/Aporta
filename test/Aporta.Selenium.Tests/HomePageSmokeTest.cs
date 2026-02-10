using System.Diagnostics;
using System.Net;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
namespace Aporta.Selenium.Tests;

[TestFixture]
public class HomePageSmokeTest
{
    private const string BaseUrl = "https://localhost:5001";
    private const int AportaStartupTimeoutSeconds = 60;

    private static readonly string AportaPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "git", "Aporta");

    private static readonly string ScreenshotDir =
        Path.Combine(AportaPath, "test", "Aporta.Selenium.Tests", "screenshots");

    private Process? _aportaProcess;
    private ChromeDriver? _driver;

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

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start Aporta
        var dotnetPath = FindDotnet();
        var aportaProjectPath = Path.Combine(AportaPath, "src", "Aporta");

        Assert.That(Directory.Exists(aportaProjectPath), Is.True,
            $"Aporta project not found at {aportaProjectPath}");

        var psi = new ProcessStartInfo(dotnetPath,
            $"run --project \"{aportaProjectPath}\" -- --cleanDatabase true")
        {
            WorkingDirectory = AportaPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _aportaProcess = Process.Start(psi);
        Assert.That(_aportaProcess, Is.Not.Null, "Failed to start Aporta process");

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
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        using var pollClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(AportaStartupTimeoutSeconds);
        var ready = false;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await pollClient.GetAsync($"{BaseUrl}/door/list");
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.OK)
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

        // Set up ChromeDriver (Selenium Manager auto-downloads matching driver)
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AcceptInsecureCertificates = true;

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _driver?.Quit();
        _driver?.Dispose();

        if (_aportaProcess is { HasExited: false })
        {
            _aportaProcess.Kill(entireProcessTree: true);
            _aportaProcess.WaitForExit(5000);
        }

        _aportaProcess?.Dispose();
    }

    [Test]
    public void HomePageLoads()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl(BaseUrl);

        // Wait for Blazor WASM to fully load and render the login screen
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
        wait.Until(d =>
        {
            try
            {
                var body = d.FindElement(By.TagName("body"));
                return body.Text.Contains("Aporta Login");
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        });

        // Log in through the UI
        var inputs = _driver.FindElements(By.CssSelector("input"));
        inputs[0].SendKeys("admin");
        inputs[1].SendKeys("pass");

        var buttons = _driver.FindElements(By.TagName("button"));
        foreach (var button in buttons)
        {
            if (button.Displayed && button.Text.Contains("Login"))
            {
                button.Click();
                break;
            }
        }

        // Wait for home page to load after login
        wait.Until(d =>
        {
            try
            {
                var body = d.FindElement(By.TagName("body"));
                return body.Text.Contains("Welcome to Aporta");
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        });

        Assert.That(_driver.Title, Is.EqualTo("Aporta"));

        var body = _driver.FindElement(By.TagName("body"));
        Assert.That(body.Text, Does.Contain("Welcome to Aporta"));

        TakeScreenshot("home_page");
        TestContext.Progress.WriteLine("Home page loaded successfully with Blazor WASM content");
    }

    private void TakeScreenshot(string name)
    {
        if (_driver == null) return;

        Directory.CreateDirectory(ScreenshotDir);
        var filePath = Path.Combine(ScreenshotDir, $"{name}.png");
        var screenshot = _driver.GetScreenshot();
        screenshot.SaveAsFile(filePath);
        TestContext.Progress.WriteLine($"Screenshot saved: {filePath}");
    }
}
