using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace DistributedQRClipboard.Tests.Selenium;

public class ClipboardSharingSeleniumTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;
    private readonly string _baseUrl;
    private readonly List<IWebDriver> _drivers = new();

    public ClipboardSharingSeleniumTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        
        // Use the actual running server instead of test server
        _baseUrl = "http://localhost:5001";
        
        _output.WriteLine($"Test base URL: {_baseUrl}");
    }

    [Fact]
    public async Task ClipboardSharing_ShouldSyncTextBetweenMultipleBrowserInstances()
    {
        // Arrange
        var driver1 = CreateWebDriver("Browser1");
        var driver2 = CreateWebDriver("Browser2");
        
        try
        {
            _output.WriteLine("Starting clipboard sharing test...");
            
            // Act - Open the application in both browsers
            driver1.Navigate().GoToUrl(_baseUrl);
            driver2.Navigate().GoToUrl(_baseUrl);
            
            // Wait for both pages to load
            await WaitForPageLoad(driver1, "Browser1");
            await WaitForPageLoad(driver2, "Browser2");
            
            // Check for JavaScript errors and missing dependencies
            await CheckJavaScriptErrors(driver1, "Browser1");
            await CheckJavaScriptErrors(driver2, "Browser2");
            
            // Wait for initial connection
            await WaitForConnectionStatus(driver1, "connected", "Browser1");
            
            // Get the session URL from browser 1
            var sessionUrlElement = driver1.FindElement(By.Id("sessionUrl"));
            var sessionUrl = sessionUrlElement.Text;
            _output.WriteLine($"Session URL from Browser1: {sessionUrl}");
            
            // Navigate browser 2 to the same session
            if (!string.IsNullOrEmpty(sessionUrl) && sessionUrl != "Generating...")
            {
                _output.WriteLine($"Navigating Browser2 to session URL: {sessionUrl}");
                driver2.Navigate().GoToUrl(sessionUrl);
                await WaitForPageLoad(driver2, "Browser2");
            }
            
            // Wait for both browsers to be connected
            await WaitForConnectionStatus(driver1, "connected", "Browser1");
            await WaitForConnectionStatus(driver2, "connected", "Browser2");
            
            // Test 1: Type text in browser 1, verify it appears in browser 2
            _output.WriteLine("Test 1: Typing text in Browser1...");
            var textArea1 = driver1.FindElement(By.Id("clipboardText"));
            var textArea2 = driver2.FindElement(By.Id("clipboardText"));
            
            const string testText1 = "Hello from Browser 1! This is a test message.";
            textArea1.Clear();
            textArea1.SendKeys(testText1);
            
            // Trigger the input event manually if needed
            ((IJavaScriptExecutor)driver1).ExecuteScript(
                "arguments[0].dispatchEvent(new Event('input', { bubbles: true }));", 
                textArea1);
            
            // Wait for text to appear in browser 2
            _output.WriteLine("Waiting for text to sync to Browser2...");
            await WaitForTextInElement(driver2, textArea2, testText1, "Browser2");
            
            // Verify the text was synced
            var browser2Text = textArea2.GetAttribute("value");
            Assert.Equal(testText1, browser2Text);
            _output.WriteLine($"✅ Text successfully synced to Browser2: {browser2Text}");
            
            // Test 2: Type text in browser 2, verify it appears in browser 1
            _output.WriteLine("Test 2: Typing text in Browser2...");
            const string testText2 = "Hello from Browser 2! This is another test message.";
            textArea2.Clear();
            textArea2.SendKeys(testText2);
            
            // Trigger the input event manually if needed
            ((IJavaScriptExecutor)driver2).ExecuteScript(
                "arguments[0].dispatchEvent(new Event('input', { bubbles: true }));", 
                textArea2);
            
            // Wait for text to appear in browser 1
            _output.WriteLine("Waiting for text to sync to Browser1...");
            await WaitForTextInElement(driver1, textArea1, testText2, "Browser1");
            
            // Verify the text was synced
            var browser1Text = textArea1.GetAttribute("value");
            Assert.Equal(testText2, browser1Text);
            _output.WriteLine($"✅ Text successfully synced to Browser1: {browser1Text}");
            
            // Test 3: Clear text in one browser, verify it clears in the other
            _output.WriteLine("Test 3: Clearing text in Browser1...");
            var clearBtn1 = driver1.FindElement(By.Id("clearTextBtn"));
            clearBtn1.Click();
            
            // Wait for text to be cleared in browser 2
            _output.WriteLine("Waiting for text to clear in Browser2...");
            await WaitForTextInElement(driver2, textArea2, "", "Browser2");
            
            // Verify the text was cleared
            var browser2ClearedText = textArea2.GetAttribute("value");
            Assert.Equal("", browser2ClearedText);
            _output.WriteLine("✅ Text successfully cleared in both browsers");
            
            _output.WriteLine("🎉 All clipboard sharing tests passed!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Test failed: {ex.Message}");
            
            // Take screenshots for debugging
            TakeScreenshot(driver1, "Browser1_Error");
            TakeScreenshot(driver2, "Browser2_Error");
            
            throw;
        }
        finally
        {
            // Check for JavaScript errors in both browsers
            await CheckJavaScriptErrors(driver1, "Browser1");
            await CheckJavaScriptErrors(driver2, "Browser2");
        }
    }

    [Fact]
    public async Task QRCodeGeneration_ShouldDisplayValidQRCode()
    {
        // Arrange
        var driver = CreateWebDriver("QRCodeTest");
        
        try
        {
            _output.WriteLine("Starting QR code generation test...");
            
            // Act
            driver.Navigate().GoToUrl(_baseUrl);
            await WaitForPageLoad(driver, "QRCodeTest");
            
            // Check for JavaScript errors and library loading first
            await CheckJavaScriptErrors(driver, "QRCodeTest");
            
            await WaitForConnectionStatus(driver, "connected", "QRCodeTest");
            
            // Wait for QR code to be generated
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var qrCodeContainer = wait.Until(d => d.FindElement(By.Id("qrcode")));
            
            // Check if QR code canvas or image is present
            await Task.Delay(2000); // Give QR code time to generate
            
            var qrCodeContent = qrCodeContainer.GetAttribute("innerHTML");
            _output.WriteLine($"QR Code container content: {qrCodeContent}");
            
            // Verify QR code is not just loading
            Assert.DoesNotContain("loading", qrCodeContent?.ToLower() ?? "");
            Assert.DoesNotContain("failed", qrCodeContent?.ToLower() ?? "");
            
            // Should contain an img element with server-generated QR code
            var hasImage = qrCodeContainer.FindElements(By.TagName("img")).Count > 0;
            var hasQRContent = !string.IsNullOrWhiteSpace(qrCodeContent) && 
                              qrCodeContent != "<div class=\"loading\"></div>";
            
            Assert.True(hasImage || hasQRContent, "QR code should be generated as server-side image");
            
            // If image exists, verify it has proper src attribute
            if (hasImage)
            {
                var imgElement = qrCodeContainer.FindElement(By.TagName("img"));
                var imgSrc = imgElement.GetAttribute("src");
                _output.WriteLine($"QR Code image src: {imgSrc}");
                Assert.Contains("/api/qrcode", imgSrc);
            }
            
            _output.WriteLine("✅ QR code generation test passed!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ QR code test failed: {ex.Message}");
            TakeScreenshot(driver, "QRCodeTest_Error");
            throw;
        }
        finally
        {
            // Check for JavaScript errors in QR code test
            await CheckJavaScriptErrors(driver, "QRCodeTest");
        }
    }

    private IWebDriver CreateWebDriver(string browserName)
    {
        var options = new ChromeOptions();
        options.AddArguments(
            "--headless",  // Run in headless mode for CI
            "--no-sandbox",
            "--disable-dev-shm-usage",
            "--disable-gpu",
            "--window-size=1280,720",
            $"--user-data-dir=/tmp/selenium_{browserName}_{Guid.NewGuid()}"
        );

        var driver = new ChromeDriver(options);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        
        _drivers.Add(driver);
        _output.WriteLine($"Created WebDriver for {browserName}");
        
        return driver;
    }

    private async Task WaitForPageLoad(IWebDriver driver, string browserName)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        
        // Wait for the main container to be present
        wait.Until(d => d.FindElement(By.ClassName("container")));
        
        // Wait for the clipboard textarea to be present
        wait.Until(d => d.FindElement(By.Id("clipboardText")));
        
        _output.WriteLine($"{browserName}: Page loaded successfully");
        
        // Give a moment for JavaScript to initialize
        await Task.Delay(2000);
    }

    private async Task WaitForConnectionStatus(IWebDriver driver, string expectedStatus, string browserName)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        
        try
        {
            wait.Until(d =>
            {
                var statusElement = d.FindElement(By.Id("connectionStatus"));
                var currentStatus = statusElement.GetAttribute("class");
                _output.WriteLine($"{browserName}: Current connection status class: {currentStatus}");
                return currentStatus?.Contains(expectedStatus) ?? false;
            });
            
            _output.WriteLine($"{browserName}: Connection status is {expectedStatus}");
        }
        catch (WebDriverTimeoutException)
        {
            var statusElement = driver.FindElement(By.Id("connectionStatus"));
            var actualStatus = statusElement.GetAttribute("class");
            _output.WriteLine($"{browserName}: Timeout waiting for status '{expectedStatus}', actual: '{actualStatus}'");
            throw;
        }
        
        // Additional wait for stability
        await Task.Delay(1000);
    }

    private async Task WaitForTextInElement(IWebDriver driver, IWebElement element, string expectedText, string browserName)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        
        try
        {
            wait.Until(d =>
            {
                var currentText = element.GetAttribute("value");
                _output.WriteLine($"{browserName}: Current text: '{currentText}', Expected: '{expectedText}'");
                return currentText == expectedText;
            });
        }
        catch (WebDriverTimeoutException)
        {
            var actualText = element.GetAttribute("value");
            _output.WriteLine($"{browserName}: Timeout waiting for text '{expectedText}', actual: '{actualText}'");
            throw new TimeoutException($"Text did not sync to {browserName}. Expected: '{expectedText}', Actual: '{actualText}'");
        }
        
        await Task.Delay(500); // Small delay for stability
    }

    private Task CheckJavaScriptErrors(IWebDriver driver, string browserName)
    {
        try
        {
            // Check for JavaScript errors in console
            var logs = driver.Manage().Logs.GetLog(LogType.Browser);
            var errors = logs.Where(log => log.Level == OpenQA.Selenium.LogLevel.Severe || log.Level == OpenQA.Selenium.LogLevel.Warning).ToList();
            
            foreach (var error in errors)
            {
                _output.WriteLine($"{browserName}: Console {error.Level}: {error.Message}");
            }

            // Check if required libraries are loaded
            var signalRLoaded = ((IJavaScriptExecutor)driver).ExecuteScript("return typeof signalR !== 'undefined';") as bool? ?? false;
            
            _output.WriteLine($"{browserName}: SignalR loaded: {signalRLoaded}");
            
            if (!signalRLoaded)
            {
                throw new Exception($"{browserName}: SignalR library is not loaded");
            }

            // Check if the main app is initialized
            var appInitialized = ((IJavaScriptExecutor)driver).ExecuteScript(
                "return window.app !== undefined || window.DistributedClipboardApp !== undefined;") as bool? ?? false;
            _output.WriteLine($"{browserName}: App initialized: {appInitialized}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"{browserName}: Error checking JavaScript: {ex.Message}");
            throw;
        }
        
        return Task.CompletedTask;
    }

    private void TakeScreenshot(IWebDriver driver, string name)
    {
        try
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var fileName = $"screenshot_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", fileName);
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            screenshot.SaveAsFile(filePath);
            _output.WriteLine($"Screenshot saved: {filePath}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to take screenshot: {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var driver in _drivers)
        {
            try
            {
                driver.Quit();
                driver.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error disposing driver: {ex.Message}");
            }
        }
    }
}
