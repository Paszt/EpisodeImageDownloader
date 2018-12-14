Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Text.RegularExpressions
Imports EpisodeImageDownloader.Infrastructure
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Namespace ViewModels

    <DataContract>
    Public Class StarzViewModel
        Inherits ViewModelBase

        Public Sub New()
            SeasonInfos = New ObservableCollection(Of SeasonInformation)
        End Sub

#Region " Properties "

        Private _showName As String
        <DataMember(Order:=1)>
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
                EpisodeListUrl = String.Empty
            End Set
        End Property

        Private _episodeListUrl As String
        <DataMember(Order:=2)>
        Public Property EpisodeListUrl As String
            Get
                Return _episodeListUrl
            End Get
            Set(value As String)
                SetProperty(_episodeListUrl, value)
            End Set
        End Property

        Private _seasonInfos As ObservableCollection(Of SeasonInformation)
        <DataMember(Order:=3)>
        Public Property SeasonInfos As ObservableCollection(Of SeasonInformation)
            Get
                Return _seasonInfos
            End Get
            Set(value As ObservableCollection(Of SeasonInformation))
                SetProperty(_seasonInfos, value)
            End Set
        End Property

#End Region

        Protected Overrides Sub DownloadImages()
            My.Application.StatusMessage = "Downloading Starz episode images"
            Dim tvData As New TvDataSeries()
            For Each seasonInfo In SeasonInfos
                Dim json = WebResources.DownloadString("https://www.starz.com/api/model.json?paths=[[""contentById""," & seasonInfo.Id &
                                                       ",""childContent"",{""from"":0,""to"":" & seasonInfo.NumberOfEpisodes - 1 &
                                                       "},[""contentId"",""contentType"",""images"",""logLine"",""order"",""properCaseTitle"",""releaseYear"",""startDate"",""title""]]]&method=get")
                Dim result As JObject = JObject.Parse(json)
                Dim contentById As JToken = result.SelectToken("jsonGraph.contentById")
                Dim epInfos As IEnumerable(Of StarzEpisodeInfo) = CType(contentById, JObject).PropertyValues.Select(Function(o) o.ToObject(Of StarzEpisodeInfo))
                For Each epInfo In epInfos
                    If epInfo.ContentId IsNot Nothing Then
                        Dim ep As New TvDataEpisode() With {
                            .EpisodeName = epInfo.EpisodeTitle,
                            .EpisodeNumber = epInfo.Number,
                            .SeasonNumber = epInfo.SeasonNumber,
                            .FirstAired = epInfo.FirstAired,
                            .Overview = epInfo.LogLine.Value,
                            .ImageUrl = epInfo.Images.Value.LandscapeBg}
                        tvData.Episodes.Add(ep)
                        DownloadImage(ep)
                    End If
                Next
            Next

            If tvData.Episodes.Count > 0 Then
                ' save each season in a separate file
                For Each seasonNo In tvData.SeasonNumbersDistinct
                    Dim seasonTvdata As New TvDataSeries With {
                        .Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
                                                            OrderBy(Function(t) t.SeasonNumber).
                                                            ThenBy(Function(t) t.EpisodeNumber).ToList()
                    }
                    seasonTvdata.SeriesInfo.SeriesName = ShowName
                    seasonTvdata.SaveToFile(ShowDownloadFolder, ShowName.MakeFileNameSafe & " " & "Season " & seasonNo)
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
            My.Application.StatusMessage = String.Empty
        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            If String.IsNullOrEmpty(extension) Then
                extension = ".jpg"
            End If
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" &
                episode.EpisodeName.MakeFileNameSafeNoSpaces() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename)
            DownloadImageAddResult(episode.ImageUrl, localPath)
        End Sub

        Protected Overrides Function CanDownloadImages() As Boolean
            Return SeasonInfos.Count > 0 AndAlso
                  SeasonInfosValid() AndAlso
                  Not String.IsNullOrEmpty(ShowName)
        End Function

        Private Function SeasonInfosValid() As Boolean
            Return SeasonInfos.Where(Function(e) e.Id.HasValue).Count() = SeasonInfos.Count()
        End Function

        Public ReadOnly Property DownloadSeasonInformationCommand As ICommand
            Get
                Return New RelayCommand(AddressOf DownloadSeasonInformation,
                                        Function()
                                            Return Not String.IsNullOrWhiteSpace(ShowName)
                                        End Function)
            End Get
        End Property

#Region " Download & Parse Season Information "

        Private Sub DownloadSeasonInformation()
            SeasonInfos.Clear()
            If String.IsNullOrWhiteSpace(EpisodeListUrl) Then
                'https://www.starz.com/series/davincisdemons
                EpisodeListUrl = "https://www.starz.com/series/" & ShowName.ToVanitySlug() & "/episodes"
            End If

            ' get show id
            Dim showId = GetShowId()
            If showId = -1 Then Exit Sub

            'get number of seasons
            Dim json = WebResources.DownloadString("https://www.starz.com/api/model.json?paths=[[""contentById""," & showId &
                                                   ",""childContent"",""length""]]&method=get")
            Dim result As JObject = JObject.Parse(json)
            Dim numberOfSeasons = CInt(DirectCast(result.SelectToken("jsonGraph.contentById").First.First.First.First.First.First("value"), JValue).Value)

            'get season ids
            json = WebResources.DownloadString("https://www.starz.com/api/model.json?paths=[[""contentById""," & showId &
                                                   ",""childContent"",{""from"":0,""to"":" & numberOfSeasons - 1 & "},""contentId""]]&method=get")
            result = JObject.Parse(json)
            Dim contentById As JToken = result.SelectToken("jsonGraph.contentById")
            Dim seasonNoCounter As Integer = 1
            For Each propertyValue In CType(contentById, JObject).PropertyValues
                Dim seasonId = propertyValue.First.First.Value(Of Integer)("value")
                If seasonId > 0 Then
                    SeasonInfos.Add(New SeasonInformation() With {.Id = seasonId, .Number = seasonNoCounter})
                    seasonNoCounter += 1
                End If
            Next

            'get season Information (number of episodes and title)
            Dim seasonNosJoined As String = String.Join(",", (From s In SeasonInfos Select s.Id))
            json = WebResources.DownloadString("https://www.starz.com/api/model.json?paths=[[""contentById"",[" & seasonNosJoined &
                                               "],[""childContent"",""title""],""length""]]&method=get")
            result = JObject.Parse(json)
            contentById = result.SelectToken("jsonGraph")
            Dim sInfos = JsonConvert.DeserializeObject(Of StarzSeasonInfos)(contentById.ToString())
            For Each dictEntry In sInfos.ContentById
                Dim sNumber = CInt(dictEntry.Key)
                Dim si = SeasonInfos.Where(Function(s) s.Id.HasValue AndAlso s.Id.Value = sNumber).First()
                si.Title = dictEntry.Value.Title.Value
                si.NumberOfEpisodes = dictEntry.Value.ChildContent.Length.Value
            Next
        End Sub

        Private Function GetShowId() As Integer
            Dim html = WebResources.DownloadString(EpisodeListUrl)
            Dim doc = CsQuery.CQ.CreateDocument(html)
            Dim canonicalLink = doc.Find("link[rel=canonical]").Attr("href")
            Dim showIdMatch = Regex.Match(canonicalLink, "\/series\/(\d+)\/", RegexOptions.IgnoreCase)
            If showIdMatch.Success Then
                Return CInt(showIdMatch.Groups(1).Value)
            Else
                MessageWindow.ShowDialog("Unable to find show id while scraping Starz website. Please double check the Episode List URL", "Unable to find show Id")
                Return -1
            End If
        End Function


#End Region

#Region " Folder "

        Public Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe)
            End If
            Return String.Empty
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public Function SeasonDownloadFolder(SeasonNumber As Integer) As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(ShowDownloadFolder,
                                       "Season " & SeasonNumber.ToString("00"))
            Else
                Return String.Empty
            End If
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder)
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

#End Region

#Region " Save/Load Season Infos "

        Public Function SeasonInfoFileExists() As Boolean
            Return IO.File.Exists(SeasonInfoFileName)
        End Function

        Public Function SeasonInfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName.MakeFileNameSafe & ".eid")
        End Function

        Public ReadOnly Property SaveSeasonInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(ShowDownloadFolder) Then
                            IO.Directory.CreateDirectory(ShowDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(SeasonInfoFileName) Then
                            IO.File.WriteAllText(SeasonInfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(SeasonInfoFileName)
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(StarzViewModel))
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        'GetIsInDesignMode is used so that the button is fully visible in the designer
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse CanDownloadImages()
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadSeasonInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        Dim newViewModel As New StarzViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(StarzViewModel))
                            newViewModel = CType(jsonSerializer.ReadObject(stream), StarzViewModel)
                        End Using
                        SeasonInfos = newViewModel.SeasonInfos
                        EpisodeListUrl = newViewModel.EpisodeListUrl
                    End Sub,
                    Function() As Boolean
                        'GetIsInDesignMode is used so that the button is fully visible in the designer
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse SeasonInfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

        Public Class SeasonInformation

            Public Property Number As Integer?
            Public Property Id As Integer?
            Public Property Title As String
            Public Property NumberOfEpisodes As Integer?

        End Class

#Region " Data Structures "

#Region " Seasons "

        <DataContract>
        Public Class StarzSeasonInfos
            <JsonProperty("contentById")>
            Public Property ContentById As Dictionary(Of String, SeasonInfo)
        End Class

        <DataContract>
        Public Class SeasonInfo
            <JsonProperty("childContent")>
            Public Property ChildContent As ChildContentLength

            <JsonProperty("title")>
            Public Property Title As AtomString

        End Class

        <DataContract>
        Public Class ChildContentLength
            <JsonProperty("length")>
            Public Property Length As AtomInteger
        End Class

#End Region

#Region " Episodes "

        <DataContract>
        Public Class StarzEpisodeInfo

            <JsonProperty("contentId")>
            Public Property ContentId As AtomInteger

            <JsonProperty("contentType")>
            Public Property ContentType As AtomString

            <JsonProperty("logLine")>
            Public Property LogLine As AtomString

            <JsonProperty("order")>
            Public Property Order As AtomInteger

            <JsonProperty("properCaseTitle")>
            Public Property ProperCaseTitle As AtomString ' "Ep 201 - Inside Out"

            <JsonProperty("releaseYear")>
            Public Property ReleaseYear As AtomString

            <JsonProperty("startDate")>
            Public Property StartDate As AtomString

            <JsonProperty("title")>
            Public Property Title As AtomString ' "Counterpart: Ep 201 - Inside Out"

            <JsonProperty("images")>
            Public Property Images As AtomImages

            Private ReadOnly titleRegex As Regex = New Regex("Ep (?<season>\d+)(?<episode>\d{2}) - (?<title>.+)")

            <JsonIgnore>
            Public ReadOnly Property Number As Integer
                Get
                    Dim titleMatch = titleRegex.Match(ProperCaseTitle.Value)
                    If titleMatch.Success Then
                        Return CInt(titleMatch.Groups("episode").Value)
                    End If
                    Return -1
                End Get
            End Property

            <JsonIgnore>
            Public ReadOnly Property SeasonNumber As Integer
                Get
                    Dim titleMatch = titleRegex.Match(ProperCaseTitle.Value)
                    If titleMatch.Success Then
                        Return CInt(titleMatch.Groups("season").Value)
                    End If
                    Return -1
                End Get
            End Property

            <JsonIgnore>
            Public ReadOnly Property EpisodeTitle As String
                Get
                    Dim titleMatch = titleRegex.Match(ProperCaseTitle.Value)
                    If titleMatch.Success Then
                        Return titleMatch.Groups("title").Value
                    End If
                    Return String.Empty
                End Get
            End Property

            Private _firstAired As String = Nothing
            <JsonIgnore>
            Public ReadOnly Property FirstAired As String
                Get
                    If String.IsNullOrEmpty(_firstAired) Then
                        If String.IsNullOrEmpty(StartDate.Value) Then
                            Dim json = WebResources.DownloadString("https://www.starz.com/api/schedule/search/" & ContentId.Value)
                            Dim scheduleItem = JsonConvert.DeserializeObject(Of StarzScheduleItem)(json)
                            Dim dte As Date
                            If Date.TryParse(scheduleItem.Start, dte) Then
                                _firstAired = dte.ToIso8601DateString
                            Else
                                _firstAired = scheduleItem.Start
                            End If
                        Else
                            Dim dte As Date
                            If Date.TryParse(StartDate.Value, dte) Then
                                _firstAired = dte.ToIso8601DateString()
                            Else
                                _firstAired = StartDate.Value
                            End If
                        End If
                    End If
                    Return _firstAired
                End Get
            End Property

        End Class

        <DataContract>
        Public Class AtomImages

            <JsonProperty("value")>
            Public Property Value As StarzImages

        End Class

        <DataContract>
        Public Class StarzImages

            <JsonProperty("landscapeBg")>
            Public Property LandscapeBg As String

        End Class


#End Region

        <DataContract>
        Public Class AtomInteger

            <JsonProperty("value")>
            Public Property Value As Integer

        End Class

        <DataContract>
        Public Class AtomString

            <JsonProperty("value")>
            Public Property Value As String

        End Class

        <DataContract>
        Public Class StarzScheduleItem

            <JsonProperty("start")>
            Public Property Start As String

            <JsonProperty("end")>
            Public Property [End] As String

        End Class

#End Region

    End Class

End Namespace
