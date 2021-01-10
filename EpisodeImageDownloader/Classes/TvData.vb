Option Strict On

Imports System.Globalization
Imports System.Xml.Serialization

<Serializable(),
 ComponentModel.DesignerCategory("Code"),
 XmlType(AnonymousType:=True),
 XmlRoot(ElementName:="Data", [Namespace]:="", IsNullable:=False)>
Partial Public Class TvDataSeries
    Inherits Object

    Public Sub New()
        SeriesInfo = New Series()
        Episodes = New List(Of TvDataEpisode)
    End Sub

    <XmlElement("Series", GetType(Series), Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property SeriesInfo As Series

    <XmlElement("Episode", GetType(TvDataEpisode), Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property Episodes As List(Of TvDataEpisode)

    Public Sub SaveToFile(Directory As String, ShowName As String)
        Dim savePath As String = IO.Path.Combine(Directory, ShowName?.MakeFileNameSafe() & ".tvxml")
        If Not IO.Directory.Exists(Directory) Then
            IO.Directory.CreateDirectory(Directory)
        End If
        If IO.File.Exists(savePath) Then
            If MessageWindow.ShowDialog("File " & savePath & " exists." & Environment.NewLine & Environment.NewLine & "Overwrite existing File?",
                                        "Overwrite existing?", True) = False Then
                Exit Sub
            End If
        End If
        Using objStreamWriter As New IO.StreamWriter(savePath)
            Dim x As New XmlSerializer([GetType]())
            x.Serialize(objStreamWriter, Me)
            objStreamWriter.Close()
        End Using
    End Sub

    Public ReadOnly Property SeasonNumbersDistinct As List(Of Integer)
        Get
            Return (From e In Episodes Order By e.SeasonNumber Select e.SeasonNumber Distinct).ToList()
        End Get
    End Property

End Class

<Serializable(),
 ComponentModel.DesignerCategory("code"),
 XmlType(AnonymousType:=True)>
Partial Public Class Series

    <XmlElement("id", Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property Id() As String

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property SeriesName() As String

End Class

<Serializable(),
 ComponentModel.DesignerCategory("code"),
 XmlType(AnonymousType:=True)>
Partial Public Class TvDataEpisode
    Inherits Object

#Region " Fields "

    Private idField As String

    Private episodeNameField As String

    Private episodeNumberField As Integer

    Private firstAiredField As String

    Private overviewField As String

    Private seasonNumberField As Integer

#End Region

#Region " Properties "

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property Id() As String
        Get
            Return idField
        End Get
        Set(value As String)
            idField = value
        End Set
    End Property

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property EpisodeName() As String
        Get
            Return episodeNameField
        End Get
        Set(value As String)
            episodeNameField = value?.Trim()
        End Set
    End Property

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property EpisodeNumber() As Integer
        Get
            Return episodeNumberField
        End Get
        Set(value As Integer)
            episodeNumberField = value
        End Set
    End Property

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property FirstAired() As String
        Get
            Return firstAiredField
        End Get
        Set(value As String)
            firstAiredField = value
        End Set
    End Property

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property Overview() As String
        Get
            Return overviewField
        End Get
        Set(value As String)
            If String.IsNullOrEmpty(value) Then
                value = String.Empty
            End If
            overviewField = value.Trim()
        End Set
    End Property

    <XmlElement(Form:=Xml.Schema.XmlSchemaForm.Unqualified)>
    Public Property SeasonNumber() As Integer
        Get
            Return seasonNumberField
        End Get
        Set(value As Integer)
            seasonNumberField = value
        End Set
    End Property

    <XmlIgnore>
    Public ReadOnly Property FirstAiredYear As Integer?
        Get
            Dim firstAiredDate As Date
            If Date.TryParse(FirstAired, firstAiredDate) Then
                Return firstAiredDate.Year
            End If
            Return Nothing
        End Get
    End Property

    <XmlIgnore>
    Public Property ImageUrl As String

#End Region

    Public Function ToFilename(Optional fileExtension As String = "jpg") As String
        Return "S" & SeasonNumber.ToString("00", CultureInfo.InvariantCulture) &
               "E" & EpisodeNumber.ToString("00", CultureInfo.InvariantCulture) & "_" &
               EpisodeName.MakeFileNameSafeNoSpaces() & "." & fileExtension
    End Function

End Class