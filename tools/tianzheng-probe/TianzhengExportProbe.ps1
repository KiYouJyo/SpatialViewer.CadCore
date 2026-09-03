[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ModulePath,

    [string[]]$MatchPattern = @(
        'TDb[A-Za-z0-9_]*(Axis|Dimension|Dim|DrawingIndex|IndexPointer|Index|Pointer)[A-Za-z0-9_]*'
    ),

    [switch]$RequireMatch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-FileRange {
    param(
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [long]$Length,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if ($Offset -lt 0 -or $Length -lt 0 -or $Offset -gt $FileLength -or $Length -gt ($FileLength - $Offset)) {
        throw "Malformed PE image: $Label is outside the file boundary."
    }
}

function Read-UInt16At {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.BinaryReader]$Reader,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    Assert-FileRange -Offset $Offset -Length 2 -FileLength $FileLength -Label $Label
    $Reader.BaseStream.Position = $Offset
    return $Reader.ReadUInt16()
}

function Read-UInt32At {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.BinaryReader]$Reader,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    Assert-FileRange -Offset $Offset -Length 4 -FileLength $FileLength -Label $Label
    $Reader.BaseStream.Position = $Offset
    return $Reader.ReadUInt32()
}

function Convert-RvaToFileOffset {
    param(
        [Parameter(Mandatory = $true)]
        [uint32]$Rva,
        [Parameter(Mandatory = $true)]
        [object[]]$Sections,
        [Parameter(Mandatory = $true)]
        [uint32]$SizeOfHeaders,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if ([uint64]$Rva -lt [uint64]$SizeOfHeaders) {
        $headerOffset = [long][uint64]$Rva
        Assert-FileRange -Offset $headerOffset -Length 1 -FileLength $FileLength -Label $Label
        return $headerOffset
    }

    foreach ($section in $Sections) {
        $virtualAddress = [uint64]$section.VirtualAddress
        $virtualSize = [uint64]$section.VirtualSize
        $rawSize = [uint64]$section.SizeOfRawData
        $span = [Math]::Max($virtualSize, $rawSize)
        $rva64 = [uint64]$Rva

        if ($span -eq 0 -or $rva64 -lt $virtualAddress -or $rva64 -ge ($virtualAddress + $span)) {
            continue
        }

        $delta = $rva64 - $virtualAddress
        if ($delta -ge $rawSize) {
            throw "Malformed PE image: $Label points into a virtual-only section range."
        }

        $offset64 = [uint64]$section.PointerToRawData + $delta
        if ($offset64 -ge [uint64]$FileLength) {
            throw "Malformed PE image: $Label resolves outside the file boundary."
        }

        return [long]$offset64
    }

    throw ('Malformed PE image: unable to map {0} RVA 0x{1:X8}.' -f $Label, $Rva)
}

function Read-AsciiZAtRva {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.BinaryReader]$Reader,
        [Parameter(Mandatory = $true)]
        [uint32]$Rva,
        [Parameter(Mandatory = $true)]
        [object[]]$Sections,
        [Parameter(Mandatory = $true)]
        [uint32]$SizeOfHeaders,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $offset = Convert-RvaToFileOffset -Rva $Rva -Sections $Sections -SizeOfHeaders $SizeOfHeaders -FileLength $FileLength -Label $Label
    $Reader.BaseStream.Position = $offset
    $bytes = [System.Collections.Generic.List[byte]]::new()

    for ($index = 0; $index -lt 4096; $index++) {
        Assert-FileRange -Offset $Reader.BaseStream.Position -Length 1 -FileLength $FileLength -Label $Label
        $value = $Reader.ReadByte()
        if ($value -eq 0) {
            return [System.Text.Encoding]::ASCII.GetString($bytes.ToArray())
        }
        $bytes.Add($value)
    }

    throw "Malformed PE image: $Label exceeds the 4096-byte symbol-name limit."
}

$resolvedPath = (Resolve-Path -LiteralPath $ModulePath -ErrorAction Stop).Path
if (-not [System.IO.File]::Exists($resolvedPath)) {
    throw 'ModulePath must point to an existing file.'
}

$regexOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
$regexTimeout = [TimeSpan]::FromMilliseconds(250)
$regexes = @(
    foreach ($pattern in $MatchPattern) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            throw 'MatchPattern entries must not be empty.'
        }
        [System.Text.RegularExpressions.Regex]::new($pattern, $regexOptions, $regexTimeout)
    }
)
if ($regexes.Count -eq 0) {
    throw 'At least one MatchPattern is required.'
}

$stream = [System.IO.File]::Open(
    $resolvedPath,
    [System.IO.FileMode]::Open,
    [System.IO.FileAccess]::Read,
    [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
)
$reader = [System.IO.BinaryReader]::new($stream, [System.Text.Encoding]::ASCII, $true)

try {
    $fileLength = $stream.Length
    if ($fileLength -lt 64) {
        throw 'Malformed PE image: file is too small.'
    }

    $dosMagic = Read-UInt16At -Reader $reader -Offset 0 -FileLength $fileLength -Label 'DOS header'
    if ($dosMagic -ne 0x5A4D) {
        throw 'Malformed PE image: missing MZ signature.'
    }

    $peOffset = [long](Read-UInt32At -Reader $reader -Offset 0x3C -FileLength $fileLength -Label 'PE header pointer')
    Assert-FileRange -Offset $peOffset -Length 24 -FileLength $fileLength -Label 'PE/COFF header'
    $peSignature = Read-UInt32At -Reader $reader -Offset $peOffset -FileLength $fileLength -Label 'PE signature'
    if ($peSignature -ne 0x00004550) {
        throw 'Malformed PE image: missing PE signature.'
    }

    $coffOffset = $peOffset + 4
    $machine = Read-UInt16At -Reader $reader -Offset $coffOffset -FileLength $fileLength -Label 'COFF machine'
    $numberOfSections = Read-UInt16At -Reader $reader -Offset ($coffOffset + 2) -FileLength $fileLength -Label 'COFF section count'
    $timeDateStamp = Read-UInt32At -Reader $reader -Offset ($coffOffset + 4) -FileLength $fileLength -Label 'COFF timestamp'
    $sizeOfOptionalHeader = Read-UInt16At -Reader $reader -Offset ($coffOffset + 16) -FileLength $fileLength -Label 'COFF optional-header size'

    if ($numberOfSections -eq 0 -or $numberOfSections -gt 512) {
        throw "Malformed PE image: unreasonable section count $numberOfSections."
    }

    $optionalHeaderOffset = $coffOffset + 20
    Assert-FileRange -Offset $optionalHeaderOffset -Length $sizeOfOptionalHeader -FileLength $fileLength -Label 'optional header'
    $optionalMagic = Read-UInt16At -Reader $reader -Offset $optionalHeaderOffset -FileLength $fileLength -Label 'optional-header magic'
    switch ($optionalMagic) {
        0x10B { $dataDirectoryOffset = $optionalHeaderOffset + 96 }
        0x20B { $dataDirectoryOffset = $optionalHeaderOffset + 112 }
        default { throw ('Unsupported PE optional-header magic 0x{0:X4}.' -f $optionalMagic) }
    }

    if ($dataDirectoryOffset + 8 -gt $optionalHeaderOffset + $sizeOfOptionalHeader) {
        throw 'Malformed PE image: export data directory is absent from the optional header.'
    }

    $sizeOfHeaders = Read-UInt32At -Reader $reader -Offset ($optionalHeaderOffset + 60) -FileLength $fileLength -Label 'SizeOfHeaders'
    $exportRva = Read-UInt32At -Reader $reader -Offset $dataDirectoryOffset -FileLength $fileLength -Label 'export-table RVA'
    $exportSize = Read-UInt32At -Reader $reader -Offset ($dataDirectoryOffset + 4) -FileLength $fileLength -Label 'export-table size'

    $sectionTableOffset = $optionalHeaderOffset + $sizeOfOptionalHeader
    $sectionTableLength = [long]$numberOfSections * 40
    Assert-FileRange -Offset $sectionTableOffset -Length $sectionTableLength -FileLength $fileLength -Label 'section table'

    $sections = @()
    for ($sectionIndex = 0; $sectionIndex -lt $numberOfSections; $sectionIndex++) {
        $sectionOffset = $sectionTableOffset + ($sectionIndex * 40)
        $sections += [pscustomobject]@{
            VirtualSize = Read-UInt32At -Reader $reader -Offset ($sectionOffset + 8) -FileLength $fileLength -Label "section[$sectionIndex].VirtualSize"
            VirtualAddress = Read-UInt32At -Reader $reader -Offset ($sectionOffset + 12) -FileLength $fileLength -Label "section[$sectionIndex].VirtualAddress"
            SizeOfRawData = Read-UInt32At -Reader $reader -Offset ($sectionOffset + 16) -FileLength $fileLength -Label "section[$sectionIndex].SizeOfRawData"
            PointerToRawData = Read-UInt32At -Reader $reader -Offset ($sectionOffset + 20) -FileLength $fileLength -Label "section[$sectionIndex].PointerToRawData"
        }
    }

    $allExports = [System.Collections.Generic.List[string]]::new()
    if ($exportRva -ne 0 -and $exportSize -ne 0) {
        $exportOffset = Convert-RvaToFileOffset -Rva $exportRva -Sections $sections -SizeOfHeaders $sizeOfHeaders -FileLength $fileLength -Label 'export directory'
        Assert-FileRange -Offset $exportOffset -Length 40 -FileLength $fileLength -Label 'export directory'

        $numberOfNames = Read-UInt32At -Reader $reader -Offset ($exportOffset + 24) -FileLength $fileLength -Label 'export name count'
        $addressOfNamesRva = Read-UInt32At -Reader $reader -Offset ($exportOffset + 32) -FileLength $fileLength -Label 'export name table RVA'
        if ($numberOfNames -gt 1000000) {
            throw "Malformed PE image: unreasonable export-name count $numberOfNames."
        }

        if ($numberOfNames -gt 0) {
            if ($addressOfNamesRva -eq 0) {
                throw 'Malformed PE image: export names exist but AddressOfNames is zero.'
            }

            $nameTableOffset = Convert-RvaToFileOffset -Rva $addressOfNamesRva -Sections $sections -SizeOfHeaders $sizeOfHeaders -FileLength $fileLength -Label 'export name table'
            Assert-FileRange -Offset $nameTableOffset -Length ([long]$numberOfNames * 4) -FileLength $fileLength -Label 'export name table'

            for ([uint32]$nameIndex = 0; $nameIndex -lt $numberOfNames; $nameIndex++) {
                $nameRva = Read-UInt32At -Reader $reader -Offset ($nameTableOffset + ([long]$nameIndex * 4)) -FileLength $fileLength -Label "export name RVA[$nameIndex]"
                if ($nameRva -eq 0) {
                    throw "Malformed PE image: export name RVA[$nameIndex] is zero."
                }
                $name = Read-AsciiZAtRva -Reader $reader -Rva $nameRva -Sections $sections -SizeOfHeaders $sizeOfHeaders -FileLength $fileLength -Label "export name[$nameIndex]"
                if (-not [string]::IsNullOrEmpty($name)) {
                    $allExports.Add($name)
                }
            }
        }
    }

    $matches = @(
        $allExports |
            Where-Object {
                $candidate = $_
                foreach ($regex in $regexes) {
                    if ($regex.IsMatch($candidate)) {
                        return $true
                    }
                }
                return $false
            } |
            Sort-Object -Unique
    )

    $moduleName = [System.IO.Path]::GetFileName($resolvedPath)
    $sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToUpperInvariant()

    Write-Output '[TCHSYM] Schema=1'
    Write-Output ("[TCHSYM] Module={0}" -f $moduleName)
    Write-Output ("[TCHSYM] SHA256={0}" -f $sha256)
    Write-Output ('[TCHSYM] Machine=0x{0:X4}' -f $machine)
    Write-Output ('[TCHSYM] PETimeDateStamp=0x{0:X8}' -f $timeDateStamp)
    Write-Output ("[TCHSYM] ExportCount={0}" -f $allExports.Count)
    Write-Output ("[TCHSYM] MatchCount={0}" -f $matches.Count)
    foreach ($match in $matches) {
        Write-Output ("[TCHSYM] Symbol={0}" -f $match)
    }

    if ($RequireMatch -and $matches.Count -eq 0) {
        throw 'No export symbols matched the requested patterns.'
    }
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}
