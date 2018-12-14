Imports System.Runtime.Serialization
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class DisneyAbcGoViewModel
        Inherits ViewModelBase

        Private tvData As TvDataSeries

#Region " Properties "

        Private _showName As String
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _moduleId As String
        Public Property ModuleId As String
            Get
                Return _moduleId
            End Get
            Set(value As String)
                SetProperty(_moduleId, value)
            End Set
        End Property

        Private _showId As String
        Public Property ShowId As String
            Get
                Return _showId
            End Get
            Set(value As String)
                SetProperty(_showId, value)
            End Set
        End Property

        'Private _brands As Dictionary(Of String, String)
        'Public ReadOnly Property Brands As Dictionary(Of String, String)
        '    Get
        '        If _brands Is Nothing Then
        '            _brands = New Dictionary(Of String, String) From {
        '                {"Disney Channel", "004"},
        '                {"Disney Junior", "008"},
        '                {"Disney XD", "009"}
        '            }
        '        End If
        '        Return _brands
        '    End Get
        'End Property

        'Private _selectedBrandValue As String
        'Public Property SelectedBrandValue As String
        '    Get
        '        Return _selectedBrandValue
        '    End Get
        '    Set(value As String)
        '        SetProperty(_selectedBrandValue, value)
        '    End Set
        'End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(ModuleId) AndAlso
                   Not String.IsNullOrWhiteSpace(ShowId) ' AndAlso
            'Not String.IsNullOrWhiteSpace(SelectedBrandValue)
        End Function

        Protected Overrides Sub DownloadImages()
            tvData = New TvDataSeries()

            My.Application.StatusMessage = "Downloading data from DisneyABC Go"
            Dim jsonStr As String = String.Empty

            Try
                jsonStr = WebResources.DownloadString(GetApiUrl())
            Catch ex As Exception
                MessageWindow.ShowDialog(ex.Message, "Error while downloading JSON")
                Exit Sub
            End Try

            Dim section = jsonStr.FromJSON(Of DisneyAbcSection)()

            My.Application.StatusMessage = "Parsing"

            For Each tile In section.Tilegroup.Tiles.Tile
                tvData.Episodes.Add(GetEpisodeFromTile(tile))
            Next

            My.Application.StatusMessage = "Downloading images"
            Parallel.ForEach(tvData.Episodes, AddressOf DownloadEpisodeImage)

            If tvData.Episodes.Count > 0 Then
                ' save each season in a separate file
                For Each seasonNo In tvData.SeasonNumbersDistinct
                    Dim seasonTvdata As New TvDataSeries With {
                        .Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
                                                            OrderBy(Function(t) t.SeasonNumber).
                                                            ThenBy(Function(t) t.EpisodeNumber).ToList()
                    }
                    seasonTvdata.SeriesInfo.SeriesName = ShowName
                    seasonTvdata.SaveToFile(ShowFolderPath, ShowName & " " & "Season " & seasonNo)
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
            My.Application.StatusMessage = String.Empty
        End Sub

        Private Sub DownloadEpisodeImage(ep As TvDataEpisode)
            'Dim extension = IO.Path.GetExtension(ep.ImageUrl)
            Dim filename = IO.Path.GetFileName(ep.ImageUrl)
            Dim SeasonEpisodeFilename = "S" & ep.SeasonNumber.ToString("00") & "E" & ep.EpisodeNumber.ToString("00") & "-" & ep.EpisodeName.MakeFileNameSafe() & "_" & filename
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(ep.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(ep.ImageUrl, localPath)
        End Sub

#Region " Helpers "

        Private Function GetApiUrl() As String
            'https://api.presentation.abc.go.com/api/ws/presentation/v2/module/2052075.json?brand=004&device=001&authlevel=1&start=0&size=100&show=SH553715218
            'https://api.presentation.watchabc.go.com/api/ws/presentation/v2/module/1973366.json?brand=011&device=001&authlevel=0&start=32&size=20&group=allages&show=SH55318965
            'Dim format = "https://api.presentation.watchabc.go.com/api/ws/presentation/v2/module/{0}.json?brand={1}&device=001&authlevel=0&start=0&size=100&group=allages&show={2}"
            'Brand no longer a variable, 011 = Disney Now
            Dim format = "https://api.presentation.watchabc.go.com/api/ws/presentation/v2/module/{0}.json?brand=011&device=001&authlevel=0&start=0&size=100&group=allages&show={1}"
            Return String.Format(format, ModuleId, ShowId)
        End Function

        Private Function GetEpisodeFromTile(tile As Tile) As TvDataEpisode
            Return New TvDataEpisode() With {
                .EpisodeName = tile.Video.Title,
                .EpisodeNumber = CInt(tile.Video.Episodenumber),
                .FirstAired = tile.Video.Airtime.ToIso8601DateString,
                .ImageUrl = GetFullSizeImage(tile),
                .Overview = tile.Video.Longdescription,
                .SeasonNumber = CInt(tile.Video.Seasonnumber)}
        End Function

        Private Function GetFullSizeImage(tile As Tile) As String
            Dim originalUrl = tile.Images.Image(0).Value
            Return Text.RegularExpressions.Regex.Replace(originalUrl, "\/\d+x\d+-Q\d+", "/1920x1080-Q100")
        End Function

#End Region

#Region " Folder methods "

        Private Function ShowFolderPath() As String
            Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe)
        End Function

        Private Function SeasonDownloadFolder(seasonNumber As Integer) As String
            Return IO.Path.Combine(ShowFolderPath, "Season " & seasonNumber.ToString("00"))
        End Function

        Private Function ShowFolderExists() As Boolean
            Return IO.Directory.Exists(ShowFolderPath)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Process.Start(ShowFolderPath)
                    End Sub, AddressOf ShowFolderExists)
            End Get
        End Property

#End Region

#Region " Data Structures "

        <DataContract>
        Public Class DisneyAbcSection

            <DataMember(Name:="tilegroup")>
            Public Property Tilegroup As Tilegroup

        End Class

        <DataContract>
        Public Class Tilegroup

            <DataMember(Name:="tiles")>
            Public Property Tiles As Tiles

        End Class

        <DataContract>
        Public Class Tiles

            <DataMember(Name:="tile")>
            Public Property Tile As List(Of Tile)

            <DataMember(Name:="total")>
            Public Property Total As String

            <DataMember(Name:="count")>
            Public Property Count As Integer
        End Class

        <DataContract>
        Public Class Tile

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="header")>
            Public Property Header As String

            <DataMember(Name:="video")>
            Public Property Video As Video

            <DataMember(Name:="images")>
            Public Property Images As Images

        End Class

        <DataContract>
        Public Class Video

            <DataMember(Name:="airtime")>
            Public Property Airtime As String

            '<DataMember(Name:="description")>
            'Public Property Description As String

            <DataMember(Name:="longdescription")>
            Public Property Longdescription As String

            <DataMember(Name:="episodenumber")>
            Public Property Episodenumber As String

            '<DataMember(Name:="images")>
            'Public Property Images As Images

            <DataMember(Name:="seasonnumber")>
            Public Property Seasonnumber As String

            <DataMember(Name:="title")>
            Public Property Title As String

        End Class

        <DataContract>
        Public Class Images

            <DataMember(Name:="image")>
            Public Property Image As List(Of DisneyAbcImage)
        End Class

        <DataContract>
        Public Class DisneyAbcImage

            <DataMember(Name:="width")>
            Public Property Width As String

            <DataMember(Name:="height")>
            Public Property Height As String

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="value")>
            Public Property Value As String

            <DataMember(Name:="ratio")>
            Public Property Ratio As String

            <DataMember(Name:="format")>
            Public Property Format As String

        End Class

#End Region

    End Class

End Namespace
