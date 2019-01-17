﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using HolisticWare.Xamarin.Tools.Bindings.XamarinAndroid.AndroidX.Migraineator.Core;
using HolisticWare.Xamarin.Tools.Bindings.XamarinAndroid.AndroidX.Migraineator.Core.Generated;

namespace Sample.Migraineator.ConsoleApp
{
    class Program
    {
        //------------------------------------------------------------------------------------------------------------------
        /*
        _ _  ____ _______ ______                                              
         | \ | |/ __ \__ __|  ____|_                                            
         |  \| | |  | | | |  | |__(_)
         | . ` | |  | | | |  |  __|                                               
         | |\  | |__| | | |  | |____ _                                            
         |_|_\_|\____/  |_|  |______(_)                       _ _   _             
         |  ____(_) |                                        (_) | (_)            
         | |__ _| | ___ _____   _____ _ ____ ___ __ _| |_ _ _ __   __ _ 
         |  __| | | |/ _ \  / _ \ \ / / _ \ '__\ \ /\ / / '__| | __| | '_ \ / _` |
         | |    | | |  __/ | (_) \ V /  __/ |   \ V V /| |  | | |_| | | | | (_| |
         |_|    |_|_|\___|  \___/ \_/ \___|_|    \_/\_/ |_|  |_|\__|_|_| |_|\__, |
                                                                             __/ |
                                                                            |___/ 
        */
        static bool overwrite_files = true;
        //------------------------------------------------------------------------------------------------------------------
        static int verbosity;
        static AndroidXDiffComparer api_info_comparer = null;

        public static void Main(string[] args)
        {
            api_info_comparer = new AndroidXDiffComparer();

            bool show_help = false;
            List<string> names = new List<string>();
            string file_input_androidx = null;
            string file_input_android_support_28_0_0 = null;
            string file_output = null;

            Mono.Options.OptionSet option_set = new Mono.Options.OptionSet()
            {
                // ../../../../../../../X/AndroidSupportComponents-AndroidX-binderate/output/AndroidSupport.api-diff.xml
                {
                    "i|input=",
                    "input folder with Android Support Diff (Metadata.xml, Metadata*.xml)",
                    (string v) =>
                    {
                        file_input_androidx = v;
                        return;
                    }
                },
                {
                    "o|output=",
                    "output folder with AndroidX Xamarin.Android Metadata files (Metadata.xml, Metadata*.xml)",
                    (string v) =>
                    {
                        file_output = v;
                        return;
                    }
                },
                {
                    "h|help",
                    "show this message and exit",
                    v => show_help = v != null
                },
            };

            List<string> extra;
            try
            {
                extra = option_set.Parse(args);
            }
            catch (Mono.Options.OptionException e)
            {
                Console.Write("AndroidX.Migraineator: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `AndroidX.Migraineator --help' for more information.");
                return;
            }

            if (show_help)
            {
                ShowHelp(option_set);
                return;
            }

            string message = "AndroidX.Migraineator";
            if (extra.Count > 0)
            {
                message = string.Join(" ", extra.ToArray());
                Debug($"Using new message: {message}");
            }
            else
            {
                Debug($"Using default message: {message}");
            }

            if (string.IsNullOrWhiteSpace(file_output))
            {
                file_output =
                    @"../../../../../../../X/AndroidSupportComponents-AndroidX-binderate/output/AndroidX.api-info.xml"
                    ;
            }
            if ( file_input_androidx == null || string.IsNullOrWhiteSpace(file_input_androidx) )
            {
                file_input_androidx =
                    @"../../../../X/AndroidSupportComponents-AndroidX-binderate/output/AndroidSupport.api-info.xml"
                    //@"../../../../../../../X/AndroidSupportComponents-AndroidX-binderate/output/AndroidSupport.api-diff.xml"
                    ;
            }
            if (file_input_android_support_28_0_0 == null || string.IsNullOrWhiteSpace(file_input_android_support_28_0_0))
            {
                file_input_android_support_28_0_0 =
                    //@"../../../../X/AndroidSupportComponents-28.0.0-binderate/output/AndroidSupport.api-info.xml"
                    @"../../../../X/AndroidSupportComponents-AndroidX-binderate/output/AndroidSupport.api-info.previous.xml"
                    ;
            }

            Task t = ProcessApiInfoFilesAsync
                            (
                                file_input_androidx, 
                                file_input_android_support_28_0_0,
                                file_output
                            );

            Task.WaitAll(t);

            api_info_comparer.GetType();

            return;
        }

        private static async Task ProcessApiInfoFilesAsync
                                                (
                                                    string file_input_androidx,
                                                    string file_input_android_support_28_0_0,
                                                    string file_output
                                                )
        {
            #if DEBUG && NETCOREAPP && NETCOREAPP2_1
            await api_info_comparer.InitializeAsync("./bin/Debug/netcoreapp2.1/mappings/");
            #elif RELEASE && NETCOREAPP && NETCOREAPP2_1
            await androidx_diff_comparer.InitializeAsync("./bin/Debug/netcoreapp2.1/mappings/");
            #else
            androidx_diff_comparer.Initialize("./mappings/");
            #endif

            api_info_comparer.ApiInfoFileNew = file_input_androidx;
            api_info_comparer.ApiInfoFileOld = file_input_android_support_28_0_0;

            api_info_comparer.ApiInfoDataNew = await api_info_comparer.ApiInfo(file_input_androidx);
            api_info_comparer.ApiInfoDataOld = await api_info_comparer.ApiInfo(file_input_android_support_28_0_0);

            await api_info_comparer.ApiInfo(file_input_android_support_28_0_0); ;

            (
                List<string> namespaces,
                List<string> namespaces_new_suspicious,
                List<string> namespaces_old_suspicious,
                List<(string ClassName, string ClassNameFullyQualified)> classes
            ) analysis_data_old;
            analysis_data_old = api_info_comparer.Analyse(api_info_comparer.ApiInfoDataOld);

            api_info_comparer.MappingApiInfoMatertial();
            api_info_comparer.ModifyApiInfo
                                        (
                                            api_info_comparer.ContentApiInfoNew,
                                            api_info_comparer.ApiInfoDataOld
                                        );

            return;
        }

        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: greet [OPTIONS]+ message");
            Console.WriteLine("Greet a list of individuals with an optional message.");
            Console.WriteLine("If no message is specified, a generic greeting is used.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);

            return;
        }

        static async Task Analyse()
        {
            /*
            (
                List<string> namespaces_28,
                List<string> namespaces_28_new_suspicious,
                List<string> namespaces_28_old_suspicious,
                List<string> classes_28
            ) = androidx_diff_comparer.Analyse(api_info_previous_androidsupport_28_0_0.api_info_previous);

            androidx_diff_comparer.DumpToFiles
            (
                api_info_previous_androidsupport_28_0_0.api_info_previous, 
                "AndroidSupport_28_0_0"
            );

            (
                string content_new,
                ApiInfo api_info_new
            )
                api_info_androidx = await androidx_diff_comparer.ApiInfo(file_input_androidx);

            (
                List<string> namespaces_x,
                List<string> namespaces_x_new_suspicious,
                List<string> namespaces_x_old_suspicious,
                List<string> classes_x
            ) = androidx_diff_comparer.Analyse(api_info_androidx.api_info_new);

            androidx_diff_comparer.DumpToFiles
            (
                api_info_androidx.api_info_new, 
                "AndroidX"
            );
            */

        }


        static void Debug(string format, params object[] args)
        {
            if (verbosity > 0)
            {
                Console.Write("# ");
                Console.WriteLine(format, args);
            }

            return;
        }
    }
}