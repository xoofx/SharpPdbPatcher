// Copyright (c) 2013 SharpPdbPatcher - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;

namespace SharpPdbPatcher
{
    /// <summary>
    /// Provides methods to patch the sourcepath location stored in a PDB file.
    /// </summary>
    public static class PdbPatcher
    {
        /// <summary>
        /// Patches the specified input PDB file.
        /// </summary>
        /// <param name="inputExeFile">The input PDB file.</param>
        /// <param name="outputPdbFile">The output PDB file.</param>
        /// <param name="regexPattern">The regex pattern.</param>
        /// <param name="replacement">The replacement.</param>
        /// <exception cref="System.ArgumentNullException">
        /// regexPattern
        /// or
        /// replacement
        /// </exception>
        public static void Patch(string inputExeFile, string outputPdbFile, Regex regexPattern, string replacement)
        {
            if (regexPattern == null) throw new ArgumentNullException("regexPattern");
            if (replacement == null) throw new ArgumentNullException("replacement");
            Patch(inputExeFile, outputPdbFile, s => regexPattern.Replace(s, replacement));
        }

        /// <summary>
        /// Patches the specified input PDB file.
        /// </summary>
        /// <param name="inputExeFile">The input PDB file.</param>
        /// <param name="outputPdbFile">The output PDB file.</param>
        /// <param name="sourcePathModifier">The source path modifier.</param>
        /// <exception cref="System.ArgumentNullException">inputExeFile</exception>
        public static void Patch(string inputExeFile, string outputPdbFile, Func<string, string> sourcePathModifier)
        {
            if (inputExeFile == null) throw new ArgumentNullException("inputExeFile");
            if (outputPdbFile == null) throw new ArgumentNullException("outputPdbFile");
            if (sourcePathModifier == null) throw new ArgumentNullException("sourcePathModifier");

            // Copy PDB from input assembly to output assembly if any
            var inputPdbFile = Path.ChangeExtension(inputExeFile, "pdb");
            if (!File.Exists(inputPdbFile))
            {
                ShowMessage(string.Format("Warning file [{0}] does not exist", inputPdbFile), ConsoleColor.Yellow);
                return;
            }

            var symbolReaderProvider = new PdbReaderProvider();
            var readerParameters = new ReaderParameters
            {
                SymbolReaderProvider = symbolReaderProvider,
                ReadSymbols = true
            };
            
            // Read Assembly
            var assembly = AssemblyDefinition.ReadAssembly(inputExeFile, readerParameters);

            // Patch the pdb
            PatchPdbToUseRelativePath(assembly, sourcePathModifier);

            // Write back the assembly and pdb
            assembly.Write(inputExeFile, new WriterParameters {WriteSymbols = true});
        }


        private static FieldInfo GetField(Type type, string name, ref FieldInfo fieldInfo)
        {
            return fieldInfo ?? (fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        /// <summary>
        /// Patches the PDB to use relative path for source file instead of absolute path.
        /// Having relative path is making the pdb much easier to redistribute (and we don't need anymore
        /// to distribute pdb files to nuget symbols server)
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        private static void PatchPdbToUseRelativePath(AssemblyDefinition assembly, Func<string, string> sourcePathModifier)
        {
            FieldInfo functionsField = null;
            FieldInfo linesField = null;
            FieldInfo fileField = null;
            FieldInfo nameField = null;

            // TODO instead of hacking Cecil to access internal methods, better modify Cecil and provide a method
            // to allow source paths renaming directly from the library API
            foreach (var module in assembly.Modules)
            {
                ISymbolReader symbols = module.SymbolReader;
                if (symbols == null)
                    continue;

                // Because we don't have access to the underlying classes, we are doing this with reflection
                var functions = (IDictionary)GetField(symbols.GetType(), "functions", ref functionsField).GetValue(symbols);

                if (functions == null)
                    return;

                foreach (var function in functions.Values)
                {
                    var lines = (object[])GetField(function.GetType(), "lines", ref linesField).GetValue(function);
                    if (lines == null)
                        continue;

                    foreach (var line in lines)
                    {
                        var source = GetField(line.GetType(), "file", ref fileField).GetValue(line);
                        if (source == null)
                            continue;

                        GetField(source.GetType(), "name", ref nameField);
                        var name = (string)nameField.GetValue(source);
                        if (name == null)
                            continue;

                        var newName = sourcePathModifier(name);
                        nameField.SetValue(source, newName);
                    }
                }
            }
        }

        internal static void ShowMessage(string message, ConsoleColor color)
        {
            if (message != null)
            {
                var backupColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ForegroundColor = backupColor;
            }
        }

        internal static void Info(string errorMessage)
        {
            ShowMessage(errorMessage, ConsoleColor.Green);
        }

        internal static void Error(string errorMessage)
        {
            ShowMessage(errorMessage, ConsoleColor.Red);
        }

        internal static void ShowHelp(string errorMessage)
        {
            Console.WriteLine("SharpPdbPatcher (c) 2013 Alexandre Mutel");
            Error(errorMessage);
            Console.WriteLine();
            Console.WriteLine("Usage: SharpPdbPatcher.exe --regex \"regexPattern\" --replace \"replaceString\" file1.dll [file2.dll ...]");
            if(errorMessage != null)
            {
                Environment.Exit(1);
            }
        }

        internal static void Main(string[] args)
        {
            // PdbPatcher.exe --regex "regexPattern" --replace ReplaceString [file1...n]

            Regex regexPattern = null;
            string replaceString = null;
            var files = new List<string>();
            for(int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if(arg == "--regex")
                {
                    i++;
                    if(i >= args.Length)
                        ShowHelp("Invalid --regex argument. Expecting regexPattern");
                    try
                    {
                        regexPattern  = new Regex(args[i], RegexOptions.IgnoreCase);
                    }
                    catch(Exception ex)
                    {
                        ShowHelp(string.Format("Invalid regex pattern '{0}': {1}", args[i], ex.Message));
                    }
                }
                else if (arg == "--replace")
                {
                    i++;
                    if (i >= args.Length)
                        ShowHelp("Invalid --replace argument. Expecting replaceString");

                    replaceString = args[i];
                }
                else
                {
                    var file = args[i];
                    if(file.StartsWith("*"))
                    {
                        files.AddRange(Directory.EnumerateFiles(Environment.CurrentDirectory, file));
                    }
                    else
                    {
                        files.Add(args[i]);
                    }
                }
            }

            if(regexPattern == null)
            {
                ShowHelp("Missing argument: --regex \"regexPattern\"");
            }

            if (replaceString == null)
            {
                ShowHelp("Missing argument: --replace \"replaceString\"");
            }

            if (files.Count == 0)
            {
                ShowHelp("Missing argument: file1.dll");
            }

            bool hasErrors = false;
            foreach(var file in files)
            {
                if(!File.Exists(file))
                {
                    Error(string.Format("File [{0}] does not exist", file));
                    hasErrors = true;
                    continue;
                }
                try
                {
                    Patch(file, file, regexPattern, replaceString);
                    Info(string.Format("File [{0}] successfully patched", file));
                }
                catch(Exception ex)
                {
                    Error(string.Format("Unexpecting error [{0}]: {1}", file, ex.Message));
                    hasErrors = true;
                }
            }

            Environment.Exit(hasErrors ? 1 : 0);
        }
    }
}