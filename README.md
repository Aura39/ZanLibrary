# ZanLibrary
File format library for Metal Gear Rising: Revengeance in C#  
Previously bundled with [DatsuEditor](https://github.com/Aura39/DatsuEditor)

# Usage
> This is just the library, intended to be used by developers, if you're looking for a tool then use [DatsuEditor](https://github.com/Aura39/DatsuEditor) that utilizes this library.  

Include the library as a dependency in your C# project.

# Contribution
Submit Pull Requests if you want to contribute code, or submit issues if there are bugs with the library.  
AI pull requests will be closed. People trying to hide the fact of AI usage will be banned.

## Writing Style
1. Namespaces should be written using one word describing the file usage purpose. (Common, Container, Rendering, etc). All file-related namespaces must be under the Formats namespace.
2. File classes must be called `FormatFile.cs` where `Format` is the file format name. (Bxm, Dat, Ctx, etc)
3. All file reading classes should use the included BinReader and BinWriter classes with no hardcoded endianness (unless the format has a specific one, like `.bxm` always being big endian)
4. All file reading classes must have 2 public static methods being Save() and Load() (accepting byte[]). Any other arguments are allowed if the class requires additional parameters (usually you might want an IsBigEndian argument)  