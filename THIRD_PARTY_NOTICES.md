# Third-party notices

`OwnedItemScanner.cs` adapts the owned-item scanning approach from Lookup Anything,
by Jesse Plamondon-Willard (Pathoschild), https://github.com/Pathoschild/StardewMods.
Reference commit: `76565e83ede4bc8b3c293f1c659032ba9c39c213`.

The adaptation adds source labels and error isolation, preserves item flavor and quality,
and avoids counting container roots when only their contents are requested.
It has no runtime dependency on the Lookup Anything assembly.

The MIT License (MIT)

Copyright 2016–2025 Jesse Plamondon-Willard (Pathoschild)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
