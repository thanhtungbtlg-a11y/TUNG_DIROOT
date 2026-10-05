[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet(2024, 2025, 2026)][int]$RevitVersion,
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$assembly = [IO.Path]::GetFullPath($AssemblyPath)
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Add-in assembly does not exist: $assembly" }
$directory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
New-Item -ItemType Directory -Force -Path $directory | Out-Null

$settings = [Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$writer = [Xml.XmlWriter]::Create([IO.Path]::GetFullPath($OutputPath), $settings)
try {
    $writer.WriteStartDocument()
    $writer.WriteStartElement('RevitAddIns')
    $writer.WriteStartElement('AddIn')
    $writer.WriteAttributeString('Type', 'Application')
    $entries = [ordered]@{
        Name = 'TUNG_DIROOT'
        Assembly = $assembly
        AddInId = '8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24'
        FullClassName = 'TungDiroot.Addin.Application'
        VendorId = 'TUNG'
        VendorDescription = 'TUNG_DIROOT'
    }
    foreach ($entry in $entries.GetEnumerator()) {
        $writer.WriteElementString($entry.Key, [string]$entry.Value)
    }
    $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndDocument()
}
finally { $writer.Dispose() }
Write-Host "Created Revit $RevitVersion manifest: $OutputPath"
