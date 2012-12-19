﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.586
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Kudu.Services {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Kudu.Services.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Value must be greater than or equal to {0}..
        /// </summary>
        internal static string ArgumentMustBeGreaterThanOrEqualTo {
            get {
                return ResourceManager.GetString("ArgumentMustBeGreaterThanOrEqualTo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found zero byte ranges. There must be at least one byte range provided..
        /// </summary>
        internal static string ByteRangeStreamContentNoRanges {
            get {
                return ResourceManager.GetString("ByteRangeStreamContentNoRanges", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The range unit &apos;{0}&apos; is not valid. The range must have a unit of &apos;{1}&apos;..
        /// </summary>
        internal static string ByteRangeStreamContentNotBytesRange {
            get {
                return ResourceManager.GetString("ByteRangeStreamContentNotBytesRange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The stream over which &apos;{0}&apos; provides a range view must have a length greater than or equal to 1..
        /// </summary>
        internal static string ByteRangeStreamEmpty {
            get {
                return ResourceManager.GetString("ByteRangeStreamEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The &apos;From&apos; value of the range must be less than or equal to {0}..
        /// </summary>
        internal static string ByteRangeStreamInvalidFrom {
            get {
                return ResourceManager.GetString("ByteRangeStreamInvalidFrom", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to None of the requested ranges ({0}) overlap with the current extent of the selected resource..
        /// </summary>
        internal static string ByteRangeStreamNoneOverlap {
            get {
                return ResourceManager.GetString("ByteRangeStreamNoneOverlap", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested range ({0}) does not overlap with the current extent of the selected resource..
        /// </summary>
        internal static string ByteRangeStreamNoOverlap {
            get {
                return ResourceManager.GetString("ByteRangeStreamNoOverlap", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The stream over which &apos;{0}&apos; provides a range view must be seekable..
        /// </summary>
        internal static string ByteRangeStreamNotSeekable {
            get {
                return ResourceManager.GetString("ByteRangeStreamNotSeekable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This is a read-only stream..
        /// </summary>
        internal static string ByteRangeStreamReadOnly {
            get {
                return ResourceManager.GetString("ByteRangeStreamReadOnly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to There&apos;s a deployment already in progress..
        /// </summary>
        internal static string Error_DeploymentInProgess {
            get {
                return ResourceManager.GetString("Error_DeploymentInProgess", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Deployment &apos;{0}&apos; not found..
        /// </summary>
        internal static string Error_DeploymentNotFound {
            get {
                return ResourceManager.GetString("Error_DeploymentNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The dumb protocol is not supported..
        /// </summary>
        internal static string Error_DumbProtocolNotSupported {
            get {
                return ResourceManager.GetString("Error_DumbProtocolNotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The json payload is empty..
        /// </summary>
        internal static string Error_EmptyPayload {
            get {
                return ResourceManager.GetString("Error_EmptyPayload", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Mismatch dropbox origin cursor..
        /// </summary>
        internal static string Error_MismatchDropboxCursor {
            get {
                return ResourceManager.GetString("Error_MismatchDropboxCursor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing repository url..
        /// </summary>
        internal static string Error_MissingRepositoryUrl {
            get {
                return ResourceManager.GetString("Error_MissingRepositoryUrl", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The json payload is unsupported..
        /// </summary>
        internal static string Error_UnsupportedFormat {
            get {
                return ResourceManager.GetString("Error_UnsupportedFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fetching changes..
        /// </summary>
        internal static string FetchingChanges {
            get {
                return ResourceManager.GetString("FetchingChanges", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}{1}  The application was terminated.{0}.
        /// </summary>
        internal static string LogStream_AppShutdown {
            get {
                return ResourceManager.GetString("LogStream_AppShutdown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}{1}  Error has occured and stream is terminated. {2}{0}.
        /// </summary>
        internal static string LogStream_Error {
            get {
                return ResourceManager.GetString("LogStream_Error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}  No new trace in the past {1} min(s).{2}.
        /// </summary>
        internal static string LogStream_Heartbeat {
            get {
                return ResourceManager.GetString("LogStream_Heartbeat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}  Stream terminated due to no new trace in the past {1} min(s).{2}.
        /// </summary>
        internal static string LogStream_Idle {
            get {
                return ResourceManager.GetString("LogStream_Idle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}  Welcome, you are now connected to log-streaming service.{1}.
        /// </summary>
        internal static string LogStream_Welcome {
            get {
                return ResourceManager.GetString("LogStream_Welcome", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Nothing to update..
        /// </summary>
        internal static string NothingToUpdate {
            get {
                return ResourceManager.GetString("NothingToUpdate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Receiving changes..
        /// </summary>
        internal static string ReceivingChanges {
            get {
                return ResourceManager.GetString("ReceivingChanges", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The setting &apos;{0}&apos; does not exist..
        /// </summary>
        internal static string SettingDoesNotExist {
            get {
                return ResourceManager.GetString("SettingDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The repository is currently busy handling another request. Please try again later..
        /// </summary>
        internal static string VfsController_Busy {
            get {
                return ResourceManager.GetString("VfsController_Busy", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The resource represents a directory which can not be updated..
        /// </summary>
        internal static string VfsController_CannotUpdateDirectory {
            get {
                return ResourceManager.GetString("VfsController_CannotUpdateDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ETag does not represent the latest state of the resource..
        /// </summary>
        internal static string VfsController_EtagMismatch {
            get {
                return ResourceManager.GetString("VfsController_EtagMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The contents of the file has changed while the operation was in progress. Please retry the request to avoid conflict..
        /// </summary>
        internal static string VfsController_fileChanged {
            get {
                return ResourceManager.GetString("VfsController_fileChanged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Updating an existing resource requires an If-Match header carrying a single, strong ETag..
        /// </summary>
        internal static string VfsController_MissingIfMatch {
            get {
                return ResourceManager.GetString("VfsController_MissingIfMatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not write to local resource &apos;{0}&apos;..
        /// </summary>
        internal static string VfsController_WriteConflict {
            get {
                return ResourceManager.GetString("VfsController_WriteConflict", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot delete directory. It is either not empty or access is not allowed..
        /// </summary>
        internal static string VfsControllerBase_CannotDeleteDirectory {
            get {
                return ResourceManager.GetString("VfsControllerBase_CannotDeleteDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ETag does not represent an existing commit ID: &apos;{0}&apos;. Updates must be based on an existing commit ID..
        /// </summary>
        internal static string VfsScmController_EtagMismatch {
            get {
                return ResourceManager.GetString("VfsScmController_EtagMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Updating an existing resource requires an If-Match header carrying a single, strong ETag based on an existing commit ID..
        /// </summary>
        internal static string VfsScmController_MissingIfMatch {
            get {
                return ResourceManager.GetString("VfsScmController_MissingIfMatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid ETag: &apos;{0}&apos;. Updating an existing resource requires a strong etag based on an existing commit ID..
        /// </summary>
        internal static string VfsScmController_WeakEtag {
            get {
                return ResourceManager.GetString("VfsScmController_WeakEtag", resourceCulture);
            }
        }
    }
}
