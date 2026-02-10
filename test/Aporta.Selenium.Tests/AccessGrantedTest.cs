using System.Diagnostics;
using System.Net;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Aporta.Selenium.Tests;

[TestFixture]
public class AccessGrantedTest
{
    private const string BaseUrl = "https://localhost:5001";
    private const int AportaStartupTimeoutSeconds = 60;
    private const string VirtualDriverGuid = "6667E442-53B2-4240-A10D-25F5E4400D83";

    private static readonly string AportaPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "git", "Aporta");

    private static readonly string ScreenshotDir =
        Path.Combine(AportaPath, "test", "Aporta.Selenium.Tests", "screenshots");

    private Process? _aportaProcess;
    private ChromeDriver? _driver;
    private readonly List<(string FilePath, string Description)> _screenshotEntries = new();

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

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AcceptInsecureCertificates = true;

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);

        // Warm up Blazor WASM — first page load downloads and compiles the runtime
        _driver.Navigate().GoToUrl(BaseUrl);
        var warmupWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
        warmupWait.Until(d =>
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

        TakeScreenshot("00_login_screen", "Login screen presented before authentication");

        // Log in through the UI
        var inputs = _driver.FindElements(By.CssSelector("input"));
        inputs[0].SendKeys("admin");
        inputs[1].SendKeys("pass");
        ClickButtonContaining("Login");

        warmupWait.Until(d =>
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

        TakeScreenshot("00_logged_in", "Home page loads after successful authentication");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        WriteScreenshotMarkdown();

        _driver?.Quit();
        _driver?.Dispose();

        if (_aportaProcess is { HasExited: false })
        {
            _aportaProcess.Kill(entireProcessTree: true);
            _aportaProcess.WaitForExit(5000);
        }

        _aportaProcess?.Dispose();
    }

    [Test, Order(1)]
    public void EnableVirtualDriver()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/drivers");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        // Wait for the drivers page to render with the Virtual driver visible
        WaitForBodyText("Virtual");

        // Find the Virtual driver row by finding the <th> with "Virtual" text
        // then navigating to its parent <tr>
        var virtualRow = FindTableRowContaining("Virtual");
        var actionToggle = virtualRow.FindElement(By.CssSelector("button.dropdown-toggle"));
        actionToggle.Click();

        // Wait for dropdown and click Enable — Blazorise renders DropdownItem as <a> tags
        WaitForVisibleDropdown();
        ClickDropdownItem("Enable");

        // Wait for the driver to be loaded — indicated by green color in the status column
        var longWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
        longWait.Until(d =>
        {
            try
            {
                var row = FindTableRowContaining("Virtual");
                var greenElements = row.FindElements(By.CssSelector("[style*='green']"));
                return greenElements.Count > 0;
            }
            catch
            {
                return false;
            }
        });

        TakeScreenshot("01_virtual_driver_enabled", "Enable the Virtual driver — green check confirms it is loaded and running");
        TestContext.Progress.WriteLine("Virtual driver enabled successfully");
    }

    [Test, Order(2)]
    public void AddVirtualReaderAndOutput()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

        // Wait for Virtual Driver Configuration page to load
        WaitForBodyText("Add Virtual Reader");

        // --- Add Virtual Reader ---
        ClickButtonContaining("Add Virtual Reader");
        WaitForModal();

        var nameInput = _driver.FindElement(By.Id("NameTextEdit"));
        nameInput.Clear();
        nameInput.SendKeys("Test Reader");
        nameInput.SendKeys(Keys.Tab);

        ClickModalButton("Add");
        WaitForModalClose();
        WaitForBodyText("Test Reader");

        // --- Add Virtual Output ---
        ClickButtonContaining("Add Virtual Output");
        WaitForModal();

        nameInput = _driver.FindElement(By.Id("NameTextEdit"));
        nameInput.Clear();
        nameInput.SendKeys("Test Output");
        nameInput.SendKeys(Keys.Tab);

        ClickModalButton("Add");
        WaitForModalClose();
        WaitForBodyText("Test Output");

        TakeScreenshot("02_reader_and_output_added", "Add a Virtual Reader and Virtual Output to the driver configuration");
        TestContext.Progress.WriteLine("Virtual reader and output added successfully");
    }

    [Test, Order(3)]
    public void AddDoor()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/doors");

        // Wait for the Add Door button to appear
        WaitForBodyText("Add Door");

        ClickButtonContaining("Add Door");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var nameInput = modal.FindElement(By.CssSelector("input"));
        nameInput.Clear();
        nameInput.SendKeys("Test Door");
        nameInput.SendKeys(Keys.Tab);

        // Select "Test Reader" for Access Reader In and "Test Actuator" for Door Strike
        var selects = modal.FindElements(By.CssSelector("select"));
        Assert.That(selects.Count, Is.GreaterThanOrEqualTo(3), "Expected at least 3 select elements in Add Door modal");

        var accessReaderInSelect = new SelectElement(selects[0]);
        SelectOptionContainingText(accessReaderInSelect, "Test Reader");

        var doorStrikeSelect = new SelectElement(selects[2]);
        SelectOptionContainingText(doorStrikeSelect, "Test Output");

        ClickModalButton("Add");
        WaitForModalClose();
        WaitForBodyText("Test Door");

        TakeScreenshot("04_door_added", "Create a door with the Test Reader as access reader and Test Output as door strike relay");
        TestContext.Progress.WriteLine("Door added successfully");
    }

    [Test, Order(4)]
    public void AddPerson()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");

        WaitForBodyText("Add Person");

        ClickButtonContaining("Add Person");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var inputs = modal.FindElements(By.CssSelector("input[type='text'],input:not([type])"));
        Assert.That(inputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 text inputs in Add Person modal");

        inputs[0].Clear();
        inputs[0].SendKeys("Test");
        inputs[0].SendKeys(Keys.Tab);

        inputs[1].Clear();
        inputs[1].SendKeys("Person");
        inputs[1].SendKeys(Keys.Tab);

        ClickModalButton("Add");
        WaitForModalClose();

        // Wait for the person to appear
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Test") && body.Text.Contains("Person");
        });

        TakeScreenshot("05_person_added", "Add a person (Test Person) on the credentials page");
        TestContext.Progress.WriteLine("Person added successfully");
    }

    [Test, Order(5)]
    public void SwipeBadgeFirstTime()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

        // Wait for the reader row to appear (uses ElementId set in Configuration.razor)
        WaitForBodyText("Test Reader");
        var readerRow = _driver.FindElement(By.Id("Reader:Test Reader"));

        var actionToggle = readerRow.FindElement(By.CssSelector("button.dropdown-toggle"));
        actionToggle.Click();

        WaitForVisibleDropdown();
        ClickDropdownItem("Swipe Badge");

        WaitForModal();

        var badgeInput = _driver.FindElement(By.Id("SwipeBadgeTextEdit"));
        badgeInput.Clear();
        badgeInput.SendKeys("12345");
        badgeInput.SendKeys(Keys.Tab);

        ClickModalButton("Swipe the badge");
        WaitForModalClose();

        // Pause for server to process the badge swipe and create the raw read event
        Thread.Sleep(2000);

        TakeScreenshot("06_badge_swiped", "Swipe badge 12345 on the Test Reader — creates a raw read event for enrollment");
        TestContext.Progress.WriteLine("Badge swiped successfully (first time - creates raw read event)");
    }

    [Test, Order(6)]
    public void EnrollPersonWithBadge()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        // Wait for the person row to appear
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Test") && body.Text.Contains("Person");
        });

        // Find the person row and click Action dropdown
        var personRow = FindTableRowContaining("Person");
        var actionToggle = personRow.FindElement(By.CssSelector("button.dropdown-toggle"));
        actionToggle.Click();

        WaitForVisibleDropdown();
        ClickDropdownItem("Enroll");

        WaitForModal();

        // Verify the badge is available in the enrollment dropdown (raw reads show "Badge 12345 (Test Reader)")
        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var selectElement = modal.FindElement(By.CssSelector("select"));
        Assert.That(selectElement.Text, Does.Contain("12345"), "Badge 12345 should be available in enrollment dropdown");
        Assert.That(selectElement.Text, Does.Contain("Test Reader"), "Reader name should appear in enrollment dropdown");

        ClickModalButton("Enroll");
        WaitForModalClose();

        // Wait for the "Successfully enrolled person" snackbar — this confirms the enrollment
        // API call completed (the modal closes before the API call, so modal close alone
        // is not sufficient).
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Successfully enrolled person");
        });

        // Reload to verify enrollment persisted (person row should show badge number)
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Test") && body.Text.Contains("Person");
        });

        // Verify the person row specifically has the badge number
        var enrolledRow = FindTableRowContaining("Person");
        Assert.That(enrolledRow.Text, Does.Contain("12345"),
            "Person row should contain badge number 12345 after enrollment");

        TakeScreenshot("07_person_enrolled", "Enroll Test Person with badge 12345 — badge number now appears in their row");
        TestContext.Progress.WriteLine("Person enrolled with badge 12345 successfully");
    }

    [Test, Order(7)]
    public void SwipeBadgeSecondTime()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

        WaitForBodyText("Test Reader");
        var readerRow = _driver.FindElement(By.Id("Reader:Test Reader"));

        var actionToggle = readerRow.FindElement(By.CssSelector("button.dropdown-toggle"));
        actionToggle.Click();

        WaitForVisibleDropdown();
        ClickDropdownItem("Swipe Badge");

        WaitForModal();

        var badgeInput = _driver.FindElement(By.Id("SwipeBadgeTextEdit"));
        badgeInput.Clear();
        badgeInput.SendKeys("12345");
        badgeInput.SendKeys(Keys.Tab);

        ClickModalButton("Swipe the badge");
        WaitForModalClose();

        // Wait for server to process
        Thread.Sleep(2000);

        TakeScreenshot("08_badge_swiped_again", "Swipe badge 12345 again — this time the credential is enrolled, triggering access decision");
        TestContext.Progress.WriteLine("Badge swiped second time (should trigger Access Granted)");
    }

    [Test, Order(8)]
    public void VerifyAccessGrantedOnMonitoringPage()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/monitoring");

        // Wait for the events table and Access Granted event to appear
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Access Granted");
        });

        var body = _driver.FindElement(By.TagName("body"));
        Assert.That(body.Text, Does.Contain("Access Granted"),
            "Expected 'Access Granted' event on the monitoring page");

        // Verify the Access Granted row shows Device and Credential info
        var accessGrantedRow = FindTableRowContaining("Access Granted");
        Assert.That(accessGrantedRow.Text, Does.Contain("Test Reader"),
            "Access Granted event should show device name 'Test Reader'");
        Assert.That(accessGrantedRow.Text, Does.Contain("Person, Test"),
            "Access Granted event should show credential name 'Person, Test'");

        TakeScreenshot("09_access_granted_verified", "Monitoring page shows Access Granted event with device and credential info — end-to-end flow complete");
        TestContext.Progress.WriteLine("Access Granted event verified on monitoring page!");
    }

    // --- Helper Methods ---

    /// <summary>
    /// Wait for the body text to contain the specified string.
    /// This is the most reliable wait strategy for Blazor WASM pages.
    /// </summary>
    private void WaitForBodyText(string text, int timeoutSeconds = 30)
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(timeoutSeconds));
        wait.Until(d =>
        {
            try
            {
                var body = d.FindElement(By.TagName("body"));
                return body.Text.Contains(text);
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Find a table row containing the specified text by iterating <tr> elements.
    /// Avoids XPath contains() which has issues with implicit waits.
    /// </summary>
    private IWebElement FindTableRowContaining(string text)
    {
        var rows = _driver!.FindElements(By.CssSelector("tbody tr"));
        foreach (var row in rows)
        {
            if (row.Text.Contains(text))
                return row;
        }

        Assert.Fail($"Could not find table row containing '{text}'");
        return null!; // unreachable
    }

    /// <summary>
    /// Click a button whose visible text contains the given string.
    /// Uses JavaScript querySelectorAll to find all buttons, then filters by text.
    /// </summary>
    private void ClickButtonContaining(string text)
    {
        var buttons = _driver!.FindElements(By.TagName("button"));
        foreach (var button in buttons)
        {
            if (button.Displayed && button.Text.Contains(text))
            {
                button.Click();
                return;
            }
        }

        Assert.Fail($"Could not find visible button containing '{text}'");
    }

    private void WaitForModal()
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(10));
        wait.Until(d => d.FindElements(By.CssSelector("div.modal.show")).Count > 0);
    }

    private void WaitForModalClose()
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(10));
        wait.Until(d => d.FindElements(By.CssSelector("div.modal.show")).Count == 0);
    }

    private void WaitForVisibleDropdown()
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(10));
        wait.Until(d => d.FindElements(By.CssSelector("div.dropdown-menu.show")).Count > 0);
    }

    /// <summary>
    /// Click a dropdown item (rendered as <a> by Blazorise) within the open dropdown menu.
    /// </summary>
    private void ClickDropdownItem(string text)
    {
        var menu = _driver!.FindElement(By.CssSelector("div.dropdown-menu.show"));
        var items = menu.FindElements(By.CssSelector("a.dropdown-item"));
        foreach (var item in items)
        {
            if (item.Text.Contains(text))
            {
                item.Click();
                return;
            }
        }

        Assert.Fail($"Could not find dropdown item containing '{text}'");
    }

    private void ClickModalButton(string buttonText)
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var modal = d.FindElement(By.CssSelector("div.modal.show"));
            var footer = modal.FindElement(By.CssSelector(".modal-footer"));
            var buttons = footer.FindElements(By.TagName("button"));
            foreach (var button in buttons)
            {
                if (button.Text.Contains(buttonText))
                {
                    button.Click();
                    return true;
                }
            }

            return false;
        });
    }

    private static void SelectOptionContainingText(SelectElement select, string text)
    {
        foreach (var option in select.Options)
        {
            if (option.Text.Contains(text))
            {
                select.SelectByText(option.Text);
                return;
            }
        }

        Assert.Fail($"Could not find option containing '{text}' in select");
    }

    private void TakeScreenshot(string name, string description = "")
    {
        if (_driver == null) return;

        Directory.CreateDirectory(ScreenshotDir);
        var fileName = $"{name}.png";
        var filePath = Path.Combine(ScreenshotDir, fileName);
        var screenshot = _driver.GetScreenshot();
        screenshot.SaveAsFile(filePath);
        TestContext.Progress.WriteLine($"Screenshot saved: {filePath}");

        _screenshotEntries.Add((fileName, description));
    }

    private void WriteScreenshotMarkdown()
    {
        if (_screenshotEntries.Count == 0) return;

        var mdPath = Path.Combine(ScreenshotDir, "AccessGrantedTest.md");
        using var writer = new StreamWriter(mdPath);
        writer.WriteLine("# Aporta Access Granted End-to-End UI Test");
        writer.WriteLine();
        writer.WriteLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        writer.WriteLine();

        foreach (var (filePath, description) in _screenshotEntries)
        {
            if (!string.IsNullOrEmpty(description))
            {
                writer.WriteLine($"## {description}");
                writer.WriteLine();
            }

            writer.WriteLine($"![{description}]({filePath})");
            writer.WriteLine();
        }
    }
}
