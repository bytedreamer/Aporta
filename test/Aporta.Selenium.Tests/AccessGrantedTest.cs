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

        WaitForBodyText("Add Credential");

        ClickButtonContaining("Add Credential");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var inputs = modal.FindElements(By.CssSelector("input[type='text'],input:not([type])"));
        Assert.That(inputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 text inputs in Add Credential modal");

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
            return body.Text.Contains("Successfully enrolled credential");
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

        // Verify Door Access column shows the door with Always schedule
        Assert.That(enrolledRow.Text, Does.Contain("Test Door (Always)"),
            "Person row should show 'Test Door (Always)' in the Door Access column after enrollment");

        TakeScreenshot("07_person_enrolled", "Enroll Test Person with badge 12345 — badge number and door access now appear in their row");
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

    [Test, Order(9)]
    public void AddSchedule()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/schedules");

        WaitForBodyText("Add Schedule");

        TakeScreenshot("10_schedules_page_empty", "Schedules page before any schedules have been created");

        ClickButtonContaining("Add Schedule");
        WaitForModal();

        // Enter schedule name
        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var nameInput = modal.FindElement(By.CssSelector("input"));
        nameInput.Clear();
        nameInput.SendKeys("Business Hours");
        nameInput.SendKeys(Keys.Tab);

        // Add a time interval
        var addIntervalButton = modal.FindElements(By.TagName("button"))
            .First(b => b.Displayed && b.Text.Contains("Add Time Interval"));
        addIntervalButton.Click();

        // Wait for the interval card to appear
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var m = d.FindElement(By.CssSelector("div.modal.show"));
            return m.FindElements(By.CssSelector(".card .card-body")).Count > 0;
        });

        // Find the interval card within the modal
        var intervalCard = modal.FindElement(By.CssSelector(".card .card-body"));

        // Check Mon through Fri — Blazorise renders Check as hidden input + visible label,
        // so click the labels (custom-control-label) instead of the inputs
        var checkboxLabels = intervalCard.FindElements(By.CssSelector("label.custom-control-label"));
        Assert.That(checkboxLabels.Count, Is.GreaterThanOrEqualTo(7), "Expected at least 7 day-of-week checkbox labels");
        for (var i = 0; i < 5; i++)
        {
            checkboxLabels[i].Click();
        }

        // Enter start and stop times
        var timeInputs = intervalCard.FindElements(By.CssSelector("input[type='text']"));
        Assert.That(timeInputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 time inputs");
        timeInputs[0].Clear();
        timeInputs[0].SendKeys("08:00");
        timeInputs[1].Clear();
        timeInputs[1].SendKeys("17:00");

        TakeScreenshot("11_schedule_modal_filled", "Add Schedule modal with Business Hours — Mon through Fri, 08:00 to 17:00");

        ClickModalButton("Save");
        WaitForModalClose();

        // Wait for the schedule to appear in the table
        WaitForBodyText("Business Hours");

        // Verify the summary shows the expected time interval
        var scheduleRow = FindTableRowContaining("Business Hours");
        Assert.That(scheduleRow.Text, Does.Contain("Mon"),
            "Schedule row should show day abbreviations");
        Assert.That(scheduleRow.Text, Does.Contain("08:00"),
            "Schedule row should show start time");
        Assert.That(scheduleRow.Text, Does.Contain("17:00"),
            "Schedule row should show stop time");

        TakeScreenshot("12_schedule_created", "Business Hours schedule created — Mon-Fri 08:00-17:00 visible in the schedules table");
        TestContext.Progress.WriteLine("Schedule 'Business Hours' added successfully");
    }

    [Test, Order(10)]
    public void CreateNotTodaySchedule()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/schedules");

        WaitForBodyText("Add Schedule");

        ClickButtonContaining("Add Schedule");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var nameInput = modal.FindElement(By.CssSelector("input"));
        nameInput.Clear();
        nameInput.SendKeys("Not Today");
        nameInput.SendKeys(Keys.Tab);

        // Add a time interval
        var addIntervalButton = modal.FindElements(By.TagName("button"))
            .First(b => b.Displayed && b.Text.Contains("Add Time Interval"));
        addIntervalButton.Click();

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var m = d.FindElement(By.CssSelector("div.modal.show"));
            return m.FindElements(By.CssSelector(".card .card-body")).Count > 0;
        });

        var intervalCard = modal.FindElement(By.CssSelector(".card .card-body"));

        // Check only a day that is NOT today
        // Checkbox indices: Mon=0, Tue=1, Wed=2, Thu=3, Fri=4, Sat=5, Sun=6
        var today = DateTime.Now.DayOfWeek;
        var todayIndex = today == DayOfWeek.Sunday ? 6 : (int)today - 1;
        var notTodayIndex = (todayIndex + 1) % 7;

        var checkboxLabels = intervalCard.FindElements(By.CssSelector("label.custom-control-label"));
        Assert.That(checkboxLabels.Count, Is.GreaterThanOrEqualTo(7), "Expected at least 7 day-of-week checkbox labels");
        checkboxLabels[notTodayIndex].Click();

        // Enter all-day time range
        var timeInputs = intervalCard.FindElements(By.CssSelector("input[type='text']"));
        Assert.That(timeInputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 time inputs");
        timeInputs[0].Clear();
        timeInputs[0].SendKeys("00:00");
        timeInputs[1].Clear();
        timeInputs[1].SendKeys("23:59");

        TakeScreenshot("13_not_today_schedule_modal", "Add 'Not Today' schedule — active only on a day that is not today");

        ClickModalButton("Save");
        WaitForModalClose();

        WaitForBodyText("Not Today");

        TakeScreenshot("14_not_today_schedule_created", "'Not Today' schedule created — will cause Access Denied when assigned");
        TestContext.Progress.WriteLine("Schedule 'Not Today' added successfully");
    }

    [Test, Order(11)]
    public void EditCredentialAccessToNotToday()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
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
        ClickDropdownItem("Edit Access");

        WaitForModal();

        // The modal should show the existing "Test Door (Always)" binding
        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var selects = modal.FindElements(By.CssSelector("select"));
        Assert.That(selects.Count, Is.GreaterThanOrEqualTo(2),
            "Expected at least 2 selects (door + schedule) in Edit Access modal");

        // Change the schedule dropdown (second select) from "Always" to "Not Today"
        var schedSelect = new SelectElement(selects[1]);
        SelectOptionContainingText(schedSelect, "Not Today");

        TakeScreenshot("15_edit_access_not_today", "Edit Access modal — changing schedule from Always to Not Today");

        ClickModalButton("Save");
        WaitForModalClose();

        // Wait for the snackbar confirmation
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Successfully updated door access");
        });

        // Reload and verify the Door Access column shows "Not Today" schedule
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Test") && body.Text.Contains("Person");
        });

        // Wait for door/schedule lookups to resolve and render in the Door Access column
        wait.Until(d =>
        {
            var row = FindTableRowContaining("Person");
            return row.Text.Contains("Test Door");
        });

        var updatedRow = FindTableRowContaining("Person");
        Assert.That(updatedRow.Text, Does.Contain("Test Door (Not Today)"),
            "Person row should show 'Test Door (Not Today)' after editing access");

        TakeScreenshot("16_credential_access_updated", "Credentials page — door access now shows 'Test Door (Not Today)'");
        TestContext.Progress.WriteLine("Credential access updated to 'Not Today' schedule");
    }

    [Test, Order(12)]
    public void SwipeBadgeThirdTime()
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

        TakeScreenshot("17_badge_swiped_third", "Swipe badge 12345 again — schedule is not active today, should trigger Access Denied");
        TestContext.Progress.WriteLine("Badge swiped third time (should trigger Access Denied)");
    }

    [Test, Order(13)]
    public void VerifyAccessDeniedOnMonitoringPage()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/monitoring");

        // Wait for the Access Denied event from the third swipe (schedule restriction).
        // The monitoring page shows newest events first. The first Access Denied row should
        // be from swipe 3 — it will include the credential name "Person, Test" because the
        // credential was recognized. (The earlier Access Denied from swipe 1 would NOT have
        // a credential name since it was an unknown badge.)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Access Denied");
        });

        var accessDeniedRow = FindTableRowContaining("Access Denied");
        Assert.That(accessDeniedRow.Text, Does.Contain("Test Reader"),
            "Access Denied event should show device name 'Test Reader'");
        Assert.That(accessDeniedRow.Text, Does.Contain("Person, Test"),
            "Access Denied event should show credential name 'Person, Test' (proves it's from the schedule-restricted swipe, not the unknown-badge swipe)");

        TakeScreenshot("18_access_denied_verified", "Monitoring page shows Access Denied — credential's schedule is not active today");
        TestContext.Progress.WriteLine("Access Denied event verified on monitoring page!");
    }

    [Test, Order(14)]
    public void AddCardFormat()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/cardformats");

        WaitForBodyText("Add Card Format");

        TakeScreenshot("20_card_formats_page_empty", "Card Formats page before any card formats have been created");

        ClickButtonContaining("Add Card Format");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));

        // Enter card format name
        var nameInput = modal.FindElement(By.CssSelector("input"));
        nameInput.Clear();
        nameInput.SendKeys("Standard 26-Bit");
        nameInput.SendKeys(Keys.Tab);

        // Set Min Bits and Max Bits (numeric inputs)
        var numericInputs = modal.FindElements(By.CssSelector("input[type='number']"));
        Assert.That(numericInputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 numeric inputs for Min/Max bits");

        // Min Bits
        numericInputs[0].Clear();
        numericInputs[0].SendKeys("26");
        numericInputs[0].SendKeys(Keys.Tab);

        // Max Bits
        numericInputs[1].Clear();
        numericInputs[1].SendKeys("26");
        numericInputs[1].SendKeys(Keys.Tab);

        // Add Element 1: Even Parity (type=1)
        ClickModalBodyButton("Add Element");
        WaitForElementCards(1);

        var elementCards = GetElementCards();
        SetElementType(elementCards[0], "Parity");
        SetElementNumericFields(elementCards[0], start: 0, length: 1);
        // Even parity: leave Odd unchecked, set src start/length
        SetParityFields(elementCards[0], srcStart: 1, srcLength: 12);

        // Add Element 2: Facility Code (type=2)
        ClickModalBodyButton("Add Element");
        WaitForElementCards(2);

        elementCards = GetElementCards();
        SetElementType(elementCards[1], "Field");
        SetElementNumericFields(elementCards[1], start: 1, length: 8);
        SetFieldType(elementCards[1], "Facility Code");

        // Add Element 3: Card Number (type=2)
        ClickModalBodyButton("Add Element");
        WaitForElementCards(3);

        elementCards = GetElementCards();
        SetElementType(elementCards[2], "Field");
        SetElementNumericFields(elementCards[2], start: 9, length: 16);
        // Card Number is default (value 0), no change needed

        // Add Element 4: Odd Parity (type=1)
        ClickModalBodyButton("Add Element");
        WaitForElementCards(4);

        elementCards = GetElementCards();
        SetElementType(elementCards[3], "Parity");
        SetElementNumericFields(elementCards[3], start: 25, length: 1);
        SetParityOdd(elementCards[3], true);
        SetParityFields(elementCards[3], srcStart: 13, srcLength: 12);

        TakeScreenshot("21_card_format_modal_filled", "Add Card Format modal with Standard 26-Bit — 4 elements: Even Parity, Facility Code, Card Number, Odd Parity");

        ClickModalButton("Save");
        WaitForModalClose();

        // Wait for the card format to appear in the table
        WaitForBodyText("Standard 26-Bit");

        var formatRow = FindTableRowContaining("Standard 26-Bit");
        Assert.That(formatRow.Text, Does.Contain("26"),
            "Card format row should show bit count");
        Assert.That(formatRow.Text, Does.Contain("4"),
            "Card format row should show 4 elements");

        TakeScreenshot("22_card_format_created", "Standard 26-Bit card format created — 26 bits, 4 elements visible in the table");
        TestContext.Progress.WriteLine("Card format 'Standard 26-Bit' added successfully");
    }

    [Test, Order(15)]
    public void AddHoliday()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/holidays");

        WaitForBodyText("Add Holiday");

        TakeScreenshot("30_holidays_page_empty", "Holidays page before any holidays have been created");

        ClickButtonContaining("Add Holiday");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));

        // Enter holiday name
        var nameInput = modal.FindElement(By.CssSelector("input[type='text']"));
        nameInput.Clear();
        nameInput.SendKeys("Christmas");
        nameInput.SendKeys(Keys.Tab);

        // Enter date — DateEdit renders as input[type='date']
        // Use JavaScript to set the value directly; SendKeys on date inputs is unreliable across browsers
        var dateInput = modal.FindElement(By.CssSelector("input[type='date']"));
        var christmasDate = $"{DateTime.Now.Year}-12-25";
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "var el = arguments[0]; var nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set; nativeSetter.call(el, arguments[1]); el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true }));",
            dateInput, christmasDate);
        dateInput.SendKeys(Keys.Tab);

        // Check "Repeats Annually" — first checkbox label in the modal
        var checkboxLabels = modal.FindElements(By.CssSelector("label.custom-control-label"));
        Assert.That(checkboxLabels.Count, Is.GreaterThanOrEqualTo(1), "Expected at least 1 checkbox label");
        checkboxLabels[0].Click();

        TakeScreenshot("31_holiday_modal_filled", "Add Holiday modal with Christmas — Dec 25, Repeats Annually");

        ClickModalButton("Save");
        WaitForModalClose();

        // Wait for the holiday to appear in the table
        WaitForBodyText("Christmas");

        var holidayRow = FindTableRowContaining("Christmas");
        Assert.That(holidayRow.Text, Does.Contain("Dec 25"),
            "Holiday row should show formatted date");
        Assert.That(holidayRow.Text, Does.Contain("Annually"),
            "Holiday row should show 'Annually' for repeating holiday");

        TakeScreenshot("32_holiday_created", "Christmas holiday created — Dec 25, Annually visible in the holidays table");
        TestContext.Progress.WriteLine("Holiday 'Christmas' added successfully");
    }

    [Test, Order(16)]
    public void CreateTodayScheduleAndReassign()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        // --- Create "Today All Day" schedule ---
        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/schedules");
        WaitForBodyText("Add Schedule");

        ClickButtonContaining("Add Schedule");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var nameInput = modal.FindElement(By.CssSelector("input"));
        nameInput.Clear();
        nameInput.SendKeys("Today All Day");
        nameInput.SendKeys(Keys.Tab);

        // Add a time interval
        var addIntervalButton = modal.FindElements(By.TagName("button"))
            .First(b => b.Displayed && b.Text.Contains("Add Time Interval"));
        addIntervalButton.Click();

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var m = d.FindElement(By.CssSelector("div.modal.show"));
            return m.FindElements(By.CssSelector(".card .card-body")).Count > 0;
        });

        var intervalCard = modal.FindElement(By.CssSelector(".card .card-body"));

        // Check only today's day-of-week
        var today = DateTime.Now.DayOfWeek;
        var todayIndex = today == DayOfWeek.Sunday ? 6 : (int)today - 1;

        var checkboxLabels = intervalCard.FindElements(By.CssSelector("label.custom-control-label"));
        Assert.That(checkboxLabels.Count, Is.GreaterThanOrEqualTo(7), "Expected at least 7 checkbox labels (days + holidays)");
        checkboxLabels[todayIndex].Click();

        // Enter all-day time range
        var timeInputs = intervalCard.FindElements(By.CssSelector("input[type='text']"));
        Assert.That(timeInputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 time inputs");
        timeInputs[0].Clear();
        timeInputs[0].SendKeys("00:00");
        timeInputs[1].Clear();
        timeInputs[1].SendKeys("23:59");

        // Holidays checkbox is NOT checked (default)

        TakeScreenshot("40_today_schedule_modal", "Add 'Today All Day' schedule — active today, holidays unchecked");

        ClickModalButton("Save");
        WaitForModalClose();

        WaitForBodyText("Today All Day");

        TakeScreenshot("41_today_schedule_created", "'Today All Day' schedule created");

        // --- Reassign credential from "Not Today" to "Today All Day" ---
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/credentials");

        wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Test") && body.Text.Contains("Person");
        });

        var personRow = FindTableRowContaining("Person");
        var actionToggle = personRow.FindElement(By.CssSelector("button.dropdown-toggle"));
        actionToggle.Click();

        WaitForVisibleDropdown();
        ClickDropdownItem("Edit Access");

        WaitForModal();

        modal = _driver.FindElement(By.CssSelector("div.modal.show"));
        var selects = modal.FindElements(By.CssSelector("select"));
        Assert.That(selects.Count, Is.GreaterThanOrEqualTo(2),
            "Expected at least 2 selects (door + schedule) in Edit Access modal");

        var schedSelect = new SelectElement(selects[1]);
        SelectOptionContainingText(schedSelect, "Today All Day");

        TakeScreenshot("42_edit_access_today", "Edit Access modal — changing schedule to 'Today All Day'");

        ClickModalButton("Save");
        WaitForModalClose();

        wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains("Successfully updated door access");
        });

        // --- Swipe badge and verify Access Granted ---
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

        WaitForBodyText("Test Reader");
        var readerRow = _driver.FindElement(By.Id("Reader:Test Reader"));

        actionToggle = readerRow.FindElement(By.CssSelector("button.dropdown-toggle"));
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

        Thread.Sleep(2000);

        // Verify Access Granted on monitoring page
        _driver.Navigate().GoToUrl($"{BaseUrl}/monitoring");
        WaitForBodyText("Access Granted");

        // Check the first row is Access Granted
        var firstRow = _driver.FindElement(By.CssSelector("tbody tr"));
        Assert.That(firstRow.Text, Does.Contain("Access Granted"),
            "First monitoring row should show 'Access Granted' after swipe with 'Today All Day' schedule");

        TakeScreenshot("43_access_granted_today_schedule", "Monitoring shows Access Granted — 'Today All Day' schedule is active");
        TestContext.Progress.WriteLine("'Today All Day' schedule created, credential reassigned, Access Granted verified");
    }

    [Test, Order(17)]
    public void CreateTodayHolidayAndVerifyDenied()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        // --- Add a holiday for today ---
        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/holidays");

        WaitForBodyText("Add Holiday");

        ClickButtonContaining("Add Holiday");
        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));

        var nameInput = modal.FindElement(By.CssSelector("input[type='text']"));
        nameInput.Clear();
        nameInput.SendKeys("Today Holiday");
        nameInput.SendKeys(Keys.Tab);

        // Set date to today
        var dateInput = modal.FindElement(By.CssSelector("input[type='date']"));
        var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "var el = arguments[0]; var nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set; nativeSetter.call(el, arguments[1]); el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true }));",
            dateInput, todayDate);
        dateInput.SendKeys(Keys.Tab);

        // Do NOT check "Repeats Annually" — leave it as a one-time holiday

        TakeScreenshot("44_today_holiday_modal", "Add 'Today Holiday' — date is today, no repeat");

        ClickModalButton("Save");
        WaitForModalClose();

        WaitForBodyText("Today Holiday");

        TakeScreenshot("45_today_holiday_created", "'Today Holiday' created — today is now a holiday");

        // --- Swipe badge and verify Access Denied ---
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

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

        Thread.Sleep(2000);

        // Verify Access Denied on monitoring page
        _driver.Navigate().GoToUrl($"{BaseUrl}/monitoring");
        WaitForBodyText("Access Denied");

        // Check the first row is Access Denied
        var firstRow = _driver.FindElement(By.CssSelector("tbody tr"));
        Assert.That(firstRow.Text, Does.Contain("Access Denied"),
            "First monitoring row should show 'Access Denied' — today is a holiday and schedule doesn't include holidays");

        TakeScreenshot("46_access_denied_holiday", "Monitoring shows Access Denied — today is a holiday, schedule has holidays=false");
        TestContext.Progress.WriteLine("Today Holiday created, Access Denied verified (schedule doesn't include holidays)");
    }

    [Test, Order(18)]
    public void EnableHolidaysOnScheduleAndVerifyGranted()
    {
        Assert.That(_driver, Is.Not.Null, "ChromeDriver was not initialized");

        // --- Enable Holidays on "Today All Day" schedule ---
        _driver!.Navigate().GoToUrl($"{BaseUrl}/configuration/schedules");

        WaitForBodyText("Today All Day");

        // Find the "Today All Day" row and click Edit
        var scheduleRow = FindTableRowContaining("Today All Day");
        var editButton = scheduleRow.FindElement(By.CssSelector("button"));
        editButton.Click();

        WaitForModal();

        var modal = _driver.FindElement(By.CssSelector("div.modal.show"));

        // Find the interval card and check the Holidays checkbox
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var m = d.FindElement(By.CssSelector("div.modal.show"));
            return m.FindElements(By.CssSelector(".card .card-body")).Count > 0;
        });

        var intervalCard = modal.FindElement(By.CssSelector(".card .card-body"));

        // The Holidays checkbox is the last label.custom-control-label in the card (after the 7 day checkboxes)
        var checkboxLabels = intervalCard.FindElements(By.CssSelector("label.custom-control-label"));
        Assert.That(checkboxLabels.Count, Is.GreaterThanOrEqualTo(8),
            "Expected at least 8 checkbox labels (7 days + Holidays)");
        var holidaysLabel = checkboxLabels.First(l => l.Text.Contains("Holiday"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", holidaysLabel);
        Thread.Sleep(500); // Let Blazor re-render

        TakeScreenshot("47_enable_holidays_on_schedule", "Edit 'Today All Day' schedule — checking Holidays checkbox");

        ClickModalButton("Save");
        WaitForModalClose();

        // Wait for the save API call to complete (snackbar confirms it)
        WaitForBodyText("Successfully updated schedule");

        TakeScreenshot("48_holidays_enabled_saved", "'Today All Day' schedule saved with Holidays enabled");

        // --- Swipe badge and verify Access Granted ---
        _driver.Navigate().GoToUrl($"{BaseUrl}/configuration/driver/{VirtualDriverGuid}");

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

        Thread.Sleep(2000);

        // Verify Access Granted on monitoring page
        _driver.Navigate().GoToUrl($"{BaseUrl}/monitoring");
        WaitForBodyText("Access Granted");

        // Check the first row is Access Granted
        var firstRow = _driver.FindElement(By.CssSelector("tbody tr"));
        Assert.That(firstRow.Text, Does.Contain("Access Granted"),
            "First monitoring row should show 'Access Granted' — schedule now includes holidays");

        TakeScreenshot("49_access_granted_holidays_enabled", "Monitoring shows Access Granted — schedule includes holidays, access is restored");
        TestContext.Progress.WriteLine("Holidays enabled on schedule, Access Granted verified");
    }

    // --- Card Format Test Helpers ---

    private void ClickModalBodyButton(string text)
    {
        var modal = _driver!.FindElement(By.CssSelector("div.modal.show"));
        var body = modal.FindElement(By.CssSelector(".modal-body"));
        var buttons = body.FindElements(By.TagName("button"));
        foreach (var button in buttons)
        {
            if (button.Displayed && button.Text.Contains(text))
            {
                button.Click();
                return;
            }
        }
        Assert.Fail($"Could not find modal body button containing '{text}'");
    }

    private void WaitForElementCards(int expectedCount)
    {
        var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(10));
        wait.Until(d =>
        {
            var modal = d.FindElement(By.CssSelector("div.modal.show"));
            var body = modal.FindElement(By.CssSelector(".modal-body"));
            return body.FindElements(By.CssSelector(".card .card-body")).Count >= expectedCount;
        });
    }

    private IReadOnlyList<IWebElement> GetElementCards()
    {
        var modal = _driver!.FindElement(By.CssSelector("div.modal.show"));
        var body = modal.FindElement(By.CssSelector(".modal-body"));
        return body.FindElements(By.CssSelector(".card .card-body"));
    }

    private void SetElementType(IWebElement card, string typeName)
    {
        var select = new SelectElement(card.FindElement(By.CssSelector("select")));
        select.SelectByText(typeName);
        // Allow Blazor to re-render after type change
        Thread.Sleep(500);
    }

    private void SetElementNumericFields(IWebElement card, int start, int length)
    {
        var numInputs = card.FindElements(By.CssSelector("input[type='number']"));
        Assert.That(numInputs.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 numeric inputs in element card");
        numInputs[0].Clear();
        numInputs[0].SendKeys(start.ToString());
        numInputs[0].SendKeys(Keys.Tab);
        numInputs[1].Clear();
        numInputs[1].SendKeys(length.ToString());
        numInputs[1].SendKeys(Keys.Tab);
    }

    private void SetFieldType(IWebElement card, string fieldName)
    {
        // After type change, a second select appears for Field type
        var selects = card.FindElements(By.CssSelector("select"));
        Assert.That(selects.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 selects for Field element");
        var fieldSelect = new SelectElement(selects[1]);
        fieldSelect.SelectByText(fieldName);
    }

    private void SetParityOdd(IWebElement card, bool odd)
    {
        if (!odd) return;
        var checkboxLabels = card.FindElements(By.CssSelector("label.custom-control-label"));
        if (checkboxLabels.Count > 0)
        {
            checkboxLabels[0].Click();
        }
    }

    private void SetParityFields(IWebElement card, int srcStart, int srcLength)
    {
        // After type=Parity, additional numeric inputs appear for Src Start and Src Length
        var numInputs = card.FindElements(By.CssSelector("input[type='number']"));
        // First 2 are Start Bit and Length, next 2 are Src Start and Src Length
        Assert.That(numInputs.Count, Is.GreaterThanOrEqualTo(4), "Expected at least 4 numeric inputs for Parity element");
        numInputs[2].Clear();
        numInputs[2].SendKeys(srcStart.ToString());
        numInputs[2].SendKeys(Keys.Tab);
        numInputs[3].Clear();
        numInputs[3].SendKeys(srcLength.ToString());
        numInputs[3].SendKeys(Keys.Tab);
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
