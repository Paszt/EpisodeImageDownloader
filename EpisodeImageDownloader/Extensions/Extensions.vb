Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions

Public Module ExtensionHelpers

    <Extension()>
    Public Function MakeFileNameSafe(fileName As String) As String
        If String.IsNullOrEmpty(fileName) Then
            Return String.Empty
        End If
        fileName = fileName.Replace("""", "''")
        Return IO.Path.GetInvalidFileNameChars().Aggregate(fileName, Function(current, c) current.Replace(c, "-"c))
    End Function

    <Extension()>
    Public Function MakeFileNameSafeNoSpaces(fileName As String) As String
        Return fileName.MakeFileNameSafe().Replace(" ", "-")
    End Function

    '<Extension>
    'Public Sub SaveToFile(TvData As TvDataSeries, Directory As String, ShowName As String)
    '    Dim savePath As String = IO.Path.Combine(Directory, ShowName.MakeSafeFileName() & ".tvxml")
    '    If Not IO.Directory.Exists(Directory) Then
    '        IO.Directory.CreateDirectory(Directory)
    '    End If
    '    If IO.File.Exists(savePath) Then
    '        If MessageWindow.ShowDialog("File " & savePath & " exists." & Environment.NewLine & Environment.NewLine & "Overwrite existing File?",
    '                                    "Overwrite existing?", True) = False Then
    '            Exit Sub
    '        End If
    '    End If
    '    Using objStreamWriter As New IO.StreamWriter(savePath)
    '        Dim x As New Xml.Serialization.XmlSerializer(TvData.GetType())
    '        x.Serialize(objStreamWriter, TvData)
    '        objStreamWriter.Close()
    '    End Using
    'End Sub

    <Extension>
    Public Function ToIso8601DateString(originalDateString As String) As String
        Dim dte As DateTime
        If DateTime.TryParse(originalDateString, dte) Then
            Return dte.ToString("yyyy-MM-dd")
        Else
            Dim modifiedDateString = originalDateString
            For Each tz In timeZones
                modifiedDateString = modifiedDateString.Replace(tz.Key, tz.Value)
            Next
            If DateTime.TryParse(modifiedDateString, dte) Then
                Return dte.ToString("yyyy-MM-dd")
            Else
                Return originalDateString
            End If
        End If
    End Function

    <Extension>
    Public Function ToIso8601DateString(originalDate As Date) As String
        Return originalDate.ToString("yyyy-MM-dd")
    End Function

    ''' <summary>
    ''' return a slug, lowercase string with spaces converted to hyphens and invalid URL characters removed
    ''' </summary>
    ''' <param name="phrase">The string to convert</param>
    ''' <returns></returns>
    <Extension>
    Public Function ToSlug(phrase As String) As String
        Dim str As String = phrase.RemoveAccent().ToLower()
        ' invalid chars           
        str = Regex.Replace(str, "[^a-z0-9\s-]", String.Empty)
        ' convert multiple spaces into one space   
        str = Regex.Replace(str, "\s+", " ").Trim()
        '' cut and trim 
        'str = str.Substring(0, If(str.Length <= 45, str.Length, 45)).Trim()
        ' replace spaces with hyphens 
        str = Regex.Replace(str, "\s", "-")
        Return str
    End Function

    ''' <summary>
    ''' returns a lowercase string with all spaces and invalid URL characters removed 
    ''' </summary>
    ''' <param name="phrase">The string to convert</param>
    <Extension>
    Public Function ToVanitySlug(phrase As String) As String
        Dim str As String = phrase.RemoveAccent().ToLower()
        ' invalid chars           
        str = Regex.Replace(str, "[^a-z0-9\s-]", String.Empty)
        ' remove spaces
        str = Regex.Replace(str, "\s+", String.Empty).Trim()
        Return str
    End Function

    <Extension>
    Public Function RemoveAccent(txt As String) As String
        Dim bytes As Byte() = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(txt)
        Return Text.Encoding.ASCII.GetString(bytes)
    End Function

    <Extension>
    Public Function RemoveQuerystring(url As String) As String
        Dim theUri As Uri = Nothing
        If Uri.TryCreate(url, UriKind.Absolute, theUri) Then
            Return theUri.GetLeftPart(UriPartial.Path)
        Else
            Return url
        End If
    End Function

    <Extension>
    Public Function IsInteger(str As String) As Boolean
        Dim out As Integer
        Return Integer.TryParse(str, out)
    End Function

    Private timeZones As Dictionary(Of String, String) = New Dictionary(Of String, String)() From {
        {"ACDT", "+1030"},
        {"ACST", "+0930"},
        {"ADT", "-0300"},
        {"AEDT", "+1100"},
        {"AEST", "+1000"},
        {"AHDT", "-0900"},
        {"AHST", "-1000"},
        {"AST", "-0400"},
        {"AT", "-0200"},
        {"AWDT", "+0900"},
        {"AWST", "+0800"},
        {"BAT", "+0300"},
        {"BDST", "+0200"},
        {"BET", "-1100"},
        {"BST", "-0300"},
        {"BT", "+0300"},
        {"BZT2", "-0300"},
        {"CADT", "+1030"},
        {"CAST", "+0930"},
        {"CAT", "-1000"},
        {"CCT", "+0800"},
        {"CDT", "-0500"},
        {"CED", "+0200"},
        {"CET", "+0100"},
        {"CEST", "+0200"},
        {"CST", "-0600"},
        {"EAST", "+1000"},
        {"EDT", "-0400"},
        {"EED", "+0300"},
        {"EET", "+0200"},
        {"EEST", "+0300"},
        {"EST", "-0500"},
        {"FST", "+0200"},
        {"FWT", "+0100"},
        {"GMT", "GMT"},
        {"GST", "+1000"},
        {"HDT", "-0900"},
        {"HST", "-1000"},
        {"IDLE", "+1200"},
        {"IDLW", "-1200"},
        {"IST", "+0530"},
        {"IT", "+0330"},
        {"JST", "+0900"},
        {"JT", "+0700"},
        {"MDT", "-0600"},
        {"MED", "+0200"},
        {"MET", "+0100"},
        {"MEST", "+0200"},
        {"MEWT", "+0100"},
        {"MST", "-0700"},
        {"MT", "+0800"},
        {"NDT", "-0230"},
        {"NFT", "-0330"},
        {"NT", "-1100"},
        {"NST", "+0630"},
        {"NZ", "+1100"},
        {"NZST", "+1200"},
        {"NZDT", "+1300"},
        {"NZT", "+1200"},
        {"PDT", "-0700"},
        {"PST", "-0800"},
        {"ROK", "+0900"},
        {"SAD", "+1000"},
        {"SAST", "+0900"},
        {"SAT", "+0900"},
        {"SDT", "+1000"},
        {"SST", "+0200"},
        {"SWT", "+0100"},
        {"USZ3", "+0400"},
        {"USZ4", "+0500"},
        {"USZ5", "+0600"},
        {"USZ6", "+0700"},
        {"UT", "-0000"},
        {"UTC", "-0000"},
        {"UZ10", "+1100"},
        {"WAT", "-0100"},
        {"WET", "-0000"},
        {"WST", "+0800"},
        {"YDT", "-0800"},
        {"YST", "-0900"},
        {"ZP4", "+0400"},
        {"ZP5", "+0500"},
        {"ZP6", "+0600"}
    }

End Module
