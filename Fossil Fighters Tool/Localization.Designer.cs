﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Fossil_Fighters_Tool {
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
    internal class Localization {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Localization() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Fossil_Fighters_Tool.Localization", typeof(Localization).Assembly);
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
        ///   Looks up a localized string similar to Extracted: {0}.
        /// </summary>
        internal static string FileExtracted {
            get {
                return ResourceManager.GetString("FileExtracted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Extracting: {0}.
        /// </summary>
        internal static string FileExtracting {
            get {
                return ResourceManager.GetString("FileExtracting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The contents of the stream does not match the expected output size. The file may be corrupted..
        /// </summary>
        internal static string StreamIsCorrupted {
            get {
                return ResourceManager.GetString("StreamIsCorrupted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The contents of the stream is not {0} archive..
        /// </summary>
        internal static string StreamIsNotArchive {
            get {
                return ResourceManager.GetString("StreamIsNotArchive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The contents of the stream is not {0} compressed..
        /// </summary>
        internal static string StreamIsNotCompressed {
            get {
                return ResourceManager.GetString("StreamIsNotCompressed", resourceCulture);
            }
        }
    }
}
