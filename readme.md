# SharpPdbPatcher

A library/console program to replace source paths stored in a .NET PDB file.

## Applications

PDB for **Program DataBase** is used to store various information useful for debugging. For each methods/functions in your program, the PDB is storing the exact location to the source code on your disk in an absolute form.   

When you want to deploy/distribute a .NET assembly and pdb along your sources, but you don't want to use a symbol server or recompile the assemblies on the target machine, this library allows to patch the PDBs files and replace the location.

This can be used for example in a **nuget package** instead of using a symbol server (like http://symbolsource.org) in order to provide directly sources along your assemblies. This patching can run at installation time through the use of a custom powershell `install.ps1` script.    


## Command line Usage

```
   SharpPdbPatcher.exe --regex regexPattern --replace replaceString  file1.exe/dll [file2.dll...] | *.exe/dll

   --regex regexPattern    : Specify the regular expression to use when trying to match the source location
                             Warning: `\` should be escaped 
   --replace replaceString : Specify the replacement string when the regex pattern is matching
                             Warning: `\` should don't need to be escaped    
```
Note that the PDB must be placed in the same directory along the assembly.


**Example** to replace all paths starting with `".*TopDirectory\SubDirectory"` by `E:\NewTop\NewSub\`:

```
   SharpDXPdbPatcher.exe --regex .*TopDirectory\\SubDirectory\\ --replace E:\NewTop\NewSub\ MyAssembly.dll
```

## Library usage

```C#
   // Using a custom function
   PdbPatcher.Patch("MyAssembly.pdb", "MyAssemblyOutput.pdb", location => location.SubString(10)));

   // Or using regular expressions
   PdbPatcher.Patch("MyAssembly.pdb", "MyAssemblyOutput.pdb", new Regex(".*TotoDir\\TutuDir"), "Tata");

```

## Disclaimer

Though this library has been tested on a set of assemblies, there is no full guaranty that the output PDB will work in all situations. Ensure that source code step-in is still working after patching your PDBs.

## Credits

This library is using [Mono.Cecil](https://github.com/jbevain/cecil)

## Licensing

MIT
