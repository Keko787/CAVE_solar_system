using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Runs the EditMode tests from the Stadium3D menu and logs a summary. Callbacks are
    /// registered on editor load (via <see cref="InitializeOnLoadAttribute"/>) so the result is logged
    /// even across the domain reload a test run can trigger — and for runs started from the Test Runner
    /// window too. If you don't see Console output, use "Open Test Runner" for the visual result.
    /// </summary>
    [InitializeOnLoad]
    public static class StadiumTestRunner
    {
        const string TestAssembly = "Stadium3D.Tests";

        static StadiumTestRunner()
        {
            // Persisted registration: fires for every test run, survives domain reloads.
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        [MenuItem("Stadium3D/Tests/Run EditMode Tests", priority = 40)]
        public static void RunEditModeTests()
        {
            Debug.Log($"[Stadium3D] Starting EditMode test run ({TestAssembly})…");
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { TestAssembly }
                }));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Stadium3D] Could not start the test run: {e}");
            }
        }

        [MenuItem("Stadium3D/Tests/Open Test Runner", priority = 41)]
        public static void OpenTestRunner()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            Debug.Log("[Stadium3D] Opened Test Runner — switch to the EditMode tab and Run All (green/red per test).");
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var summary = $"[Stadium3D] EditMode tests: {result.PassCount} passed, {result.FailCount} failed, " +
                              $"{result.SkipCount} skipped ({result.Duration:0.00}s).";
                if (result.FailCount > 0)
                {
                    Debug.LogError(summary);
                    LogFailures(result);
                }
                else
                {
                    Debug.Log(summary);
                }
            }

            static void LogFailures(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                {
                    foreach (var child in result.Children)
                        LogFailures(child);
                }
                else if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[Stadium3D] FAILED: {result.Test.FullName}\n{result.Message}");
                }
            }
        }
    }
}
