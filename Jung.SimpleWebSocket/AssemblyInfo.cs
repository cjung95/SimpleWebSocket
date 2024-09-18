// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// In SDK-style projects such as this one, several assembly attributes that were historically
// defined in this file are now automatically added during build and populated with
// values defined in project properties. For details of which attributes are included
// and how to customize this process see: https://aka.ms/assembly-info-properties


// Setting ComVisible to false makes the types in this assembly not visible to COM
// components.  If you need to access a type in this assembly from COM, set the ComVisible
// attribute to true on that type.

[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM.

[assembly: Guid("ca34219d-7a2e-4993-ad9d-f27fda1bb9dc")]

// Make internals visible to the test project and the dynamic proxy assembly (moq)
[assembly: InternalsVisibleTo("Jung.SimpleWebSocketTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]