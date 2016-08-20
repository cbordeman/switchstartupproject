﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LucidConcepts.SwitchStartupProject
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidSwitchStartupProjectPkgString)]
    public sealed class SwitchStartupProjectPackage : Package, IVsSolutionEvents, IVsSolutionEvents4, IVsSolutionLoadEvents, IVsSelectionEvents, IVsPersistSolutionOpts
    {

        private uint solutionEventsCookie;
        private IVsSolution2 solution = null;
        private uint selectionEventsCookie;
        private IVsMonitorSelection ms = null;
        private uint debuggingCookie;
        private bool projectsAreLoadedInBatches = false;

        private StartupProjectSwitcher switcher;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Unadvise all events
            if (solution != null && solutionEventsCookie != 0)
                solution.UnadviseSolutionEvents(solutionEventsCookie);
            if (ms != null && selectionEventsCookie != 0)
                ms.UnadviseSelectionEvents(selectionEventsCookie);
        }

        #region Package Initializer

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Logger = new ActivityLogger(this);
            Logger.LogInfo("Entering initializer for: {0}", this.ToString());
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null == mcs )
            {
                Logger.LogInfo("Could not get OleMenuCommandService");
                return;
            }
            var dropdownService = new DropdownService(mcs);

            // Get VS Automation object
            var dte = (EnvDTE.DTE)GetGlobalService(typeof(EnvDTE.DTE));

            // Get solution
            solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
            if (solution != null)
            {
                // Register for solution events
                solution.AdviseSolutionEvents(this, out solutionEventsCookie);
            }

            // Get selection monitor
            ms = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            if (ms != null)
            {
                // Remember debugging UI context cookie for later
                ms.GetCmdUIContextCookie(VSConstants.UICONTEXT.Debugging_guid, out debuggingCookie);
                // Register for selection events
                ms.AdviseSelectionEvents(this, out selectionEventsCookie);
            }

            var fileChangeService = ServiceProvider.GlobalProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
            var projectHierarchyHelper = new ProjectHierarchyHelper(solution);

            switcher = new StartupProjectSwitcher(dropdownService, dte, fileChangeService, projectHierarchyHelper, this, 99, Logger);
        }
        #endregion

        #region IVsSolutionEvents Members

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            switcher.AfterCloseSolution();
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            switcher.UpdateStartupProject();
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            switcher.OpenProject(pHierarchy);
            return VSConstants.S_OK;
        }


        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            if (!projectsAreLoadedInBatches)
            {
                switcher.AfterOpenSolution();
            }
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            object propNameObj = null;
            if (pHierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_Name, out propNameObj) == VSConstants.S_OK)
            {
                string name = (string)propNameObj;
                switcher.CloseProject(pHierarchy, name);
            }
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            switcher.BeforeCloseSolution();
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSolutionEvents4

        public int OnAfterAsynchOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterChangeProjectParent(IVsHierarchy pHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterRenameProject(IVsHierarchy pHierarchy)
        {
            switcher.RenameProject(pHierarchy);
            return VSConstants.S_OK;
        }

        public int OnQueryChangeProjectParent(IVsHierarchy pHierarchy, IVsHierarchy pNewParentHier, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSolutionLoadEvents Members

        public int OnAfterBackgroundSolutionLoadComplete()
        {
            switcher.AfterOpenSolution();
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeBackgroundSolutionLoadBegins()
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            projectsAreLoadedInBatches = true;
            return VSConstants.S_OK;
        }

        public int OnBeforeOpenSolution(string pszSolutionFilename)
        {
            projectsAreLoadedInBatches = false;
            switcher.BeforeOpenSolution(pszSolutionFilename);
            return VSConstants.S_OK;
        }

        public int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle)
        {
            pfShouldDelayLoadToNextIdle = false;
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSelectionEvents Members

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            if (dwCmdUICookie == debuggingCookie)
            {
                switcher.ToggleDebuggingActive(fActive != 0);
            }
            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                switcher.UpdateStartupProject();
            }
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsPersistSolutionOpts Members

        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            switcher.OnSolutionSaved();
            return VSConstants.S_OK;
        }

        #endregion

        #region Activity Log

        public ActivityLogger Logger { get; set; }

        public class ActivityLogger
        {
            private readonly SwitchStartupProjectPackage package;

            public ActivityLogger(SwitchStartupProjectPackage package)
            {
                this.package = package;
            }

            public void LogInfo(string message, params object[] arguments)
            {
                Log(__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, message, arguments);
            }

            public void LogWarning(string message, params object[] arguments)
            {
                Log(__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING, message, arguments);
            }

            public void LogError(string message, params object[] arguments)
            {
                Log(__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, message, arguments);
            }

            private void Log(__ACTIVITYLOG_ENTRYTYPE type, string message, params object[] arguments)
            {
                var log = package.GetService(typeof (SVsActivityLog)) as IVsActivityLog;
                if (log == null) return;
                log.LogEntry((UInt32)type, "SwitchStartupProject", string.Format(CultureInfo.CurrentCulture, message, arguments));
            }
        }

        #endregion
    }
}
