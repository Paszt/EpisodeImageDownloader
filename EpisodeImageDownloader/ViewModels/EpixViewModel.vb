Option Strict On

Imports System.Text.RegularExpressions
Imports CsQuery

Namespace ViewModels

    Public Class EpixViewModel
        Inherits ViewModels.ViewModelBase

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

        Private _showUrl As String
        Public Property ShowUrl As String
            Get
                Return _showUrl
            End Get
            Set(value As String)
                SetProperty(_showUrl, value)
            End Set
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                Return IO.Path.Combine(ShowDownloadFolder, "Season " & CInt(SeasonNumber).ToString("00"))
            End Get
        End Property

        Public ReadOnly Property SeasonEpisodeDownloadFolder(SeasonNumber As Integer, EpisodeNumber As Integer) As String
            Get
                Return IO.Path.Combine(SeasonDownloadFolder(SeasonNumber), "S" & SeasonNumber.ToString("00") & "E" & EpisodeNumber.ToString("00"))
            End Get
        End Property

        Public ReadOnly Property ShowDownloadFolder As String
            Get
                If Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe)
                Else
                    Return String.Empty
                End If
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(ShowUrl)
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder)
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Protected Overrides Sub DownloadImages()
            Dim html As String
            Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                html = client.DownloadString(ShowUrl)
            End Using
            Dim cqDoc = CQ.Create(html)

            Parallel.ForEach(cqDoc("img.gdl-gallery-image").ToList(),
                             Sub(domObj As IDomObject)
                                 DownloadImage(domObj)
                             End Sub)
            'For Each domObj In cqDoc("img.gdl-gallery-image")
            '    DownloadImage(domObj)
            'Next
        End Sub

        Private Sub DownloadImage(domObj As IDomObject)
            Dim img As CQ = domObj.Cq
            Dim src = img.Attr("src")
            Dim alt = img.Attr("alt")
            Dim SeasonNo As Integer = Nothing
            Dim EpisodeNo As Integer = Nothing
            Dim seasonEpMatch = Regex.Match(alt, "episode (\d+)(\d{2})", RegexOptions.IgnoreCase)
            If seasonEpMatch.Success Then
                SeasonNo = CInt(seasonEpMatch.Groups(1).Value)
                EpisodeNo = CInt(seasonEpMatch.Groups(2).Value)
                Dim imgSizeMatch = Regex.Match(src, ".+(-\d+x\d+)\.(?:jpg|png)", RegexOptions.IgnoreCase)
                If imgSizeMatch.Success Then
                    src = src.Replace(imgSizeMatch.Groups(1).Value, String.Empty)
                    Dim imgFilename = IO.Path.GetFileName(src)
                    Dim SeasonEpisodeFilename = "S" & SeasonNo.ToString("00") & "E" & EpisodeNo.ToString("00") & "-" & imgFilename
                    Dim localPath = IO.Path.Combine(SeasonEpisodeDownloadFolder(SeasonNo, EpisodeNo), SeasonEpisodeFilename.Replace(" ", "_"))
                    DownloadImageAddResult(src, localPath)
                End If
            End If
        End Sub

    End Class

End Namespace
