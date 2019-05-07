Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions
Imports CsQuery

Namespace ViewModels


    Public Class NetflixViewModel
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

        Private _inputHtml As String
        ''' <summary> Html from any episode video page </summary>
        Public Property InputHtml As String
            Get
                Return _inputHtml
            End Get
            Set(value As String)
                SetProperty(_inputHtml, value)
            End Set
        End Property

        Private _isHtmlInputVisible As Boolean = True
        Public Property IsHtmlInputVisible As Boolean
            Get
                Return _isHtmlInputVisible
            End Get
            Set(value As Boolean)
                SetProperty(_isHtmlInputVisible, value)
                OnPropertyChanged("IsHtmlInputNotVisible")
            End Set
        End Property

        Public ReadOnly Property IsHtmlInputNotVisible As Boolean
            Get
                If ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) Then
                    Return True
                End If
                Return Not _isHtmlInputVisible
            End Get
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                Return IO.Path.Combine(ShowDownloadFolder, "Season " & CInt(SeasonNumber).ToString("00"))
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
                   Not String.IsNullOrWhiteSpace(InputHtml)
        End Function

        Protected Overrides Sub DownloadImages()
            IsHtmlInputVisible = False
            tvData = New TvDataSeries()

            Dim language As String = String.Empty

            Dim doc = CQ.CreateDocument(InputHtml)
            For Each ep In doc.Find("div.episode")
                Dim cqEp = ep.Cq
                Dim tvEp As New TvDataEpisode() With {
                    .Overview = cqEp.Find(".epsiode-synopsis").Text().Trim(),
                    .EpisodeName = cqEp.Find("h3.episode-title").Text()
                }
                'remove number+dot (1., 2., ...) from beginning of title
                tvEp.EpisodeName = Regex.Replace(tvEp.EpisodeName, "^\d+\s*.?\s+", String.Empty)
                'find season & episode numbers from alt attribute on the image
                'Dim seasonEpisodeMatch = Regex.Match(cqEp.Find("img.episode-thumbnail-image").Attr("alt"), "\.[^0-9]+(\d+)[^0-9]+(\d+)")
                'If seasonEpisodeMatch.Success Then
                '    tvEp.EpisodeNumber = CInt(seasonEpisodeMatch.Groups(1).Value)
                '    tvEp.SeasonNumber = CInt(seasonEpisodeMatch.Groups(2).Value)
                'End If

                Dim seasonEpisodeMatch As Match
                Dim imageAlt = cqEp.Find("img.episode-thumbnail-image").Attr("alt")
                For Each regLang In regexLanguageList
                    seasonEpisodeMatch = regLang.Regex.Match(imageAlt)
                    If seasonEpisodeMatch.Success Then
                        tvEp.EpisodeNumber = CInt(seasonEpisodeMatch.Groups("episodeNo").Value)
                        tvEp.SeasonNumber = CInt(seasonEpisodeMatch.Groups("seasonNo").Value)
                        language = regLang.Language
                        Exit For
                    End If
                Next

                tvData.Episodes.Add(tvEp)
                AddEpisodeImageResult(New EpisodeImageResult() With {
                                      .FileName = "S" & tvEp.SeasonNumber.ToString("00") & "E" & tvEp.EpisodeNumber.ToString("00"),
                                      .HasError = False,
                                      .NewDownload = False,
                                      .Message = "Downloaded data but skipped image"})
            Next

            If tvData.Episodes.Count > 0 Then
                ' save each season in a separate file
                For Each seasonNo In tvData.SeasonNumbersDistinct
                    Dim seasonTvdata As New TvDataSeries With {
                        .Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
                                                            OrderBy(Function(t) t.SeasonNumber).
                                                            ThenBy(Function(t) t.EpisodeNumber).ToList()
                    }
                    seasonTvdata.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & seasonNo & "_" & language)
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
        End Sub

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

        Public ReadOnly Property ShowHtmlInputCommand() As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           IsHtmlInputVisible = True
                                                       End Sub)
            End Get
        End Property

        Private ReadOnly regexLanguageList As New List(Of RegexLanguage) From {
            New RegexLanguage("episode\s(?<episodeNo>\d+)\sof\sseason\s(?<seasonNo>\d+)", "English"),
            New RegexLanguage("episodio\s(?<episodeNo>\d+)\sde la\stemporada\s(?<seasonNo>\d+)", "Spanish"),
            New RegexLanguage("episódio\s(?<episodeNo>\d+)\sda\stemporada\s(?<seasonNo>\d+)", "Portuguese"),
            New RegexLanguage("épisode\s(?<episodeNo>\d+)\sde la\ssaison\s(?<seasonNo>\d+)", "French"),
            New RegexLanguage("avsnitt\s(?<episodeNo>\d+)\sfrån\ssäsong\s(?<seasonNo>\d+)", "Swedish"),
            New RegexLanguage("episode\s(?<episodeNo>\d+)\sav\ssesong\s(?<seasonNo>\d+)", "Norwegian Bokmål"),
            New RegexLanguage("jakso\s(?<episodeNo>\d+),\skausi\s(?<seasonNo>\d+)", "Finnish"),
            New RegexLanguage("episode\s(?<episodeNo>\d+)\saf\ssæson\s(?<seasonNo>\d+)", "Danish"),
            New RegexLanguage("aflevering\s(?<episodeNo>\d+)\svan\sseizoen\s(?<seasonNo>\d+)", "Dutch"),
            New RegexLanguage("folge\s(?<episodeNo>\d+)\sder\s(?<seasonNo>\d+)\.\sstaffel\.", "German"),
            New RegexLanguage("シ﻿ー﻿ズ﻿ン(?<seasonNo>\d+)\﻿のエ﻿ピ﻿ソ﻿ー﻿ド(?<episodeNo>\d+)", "Japanese"),
            New RegexLanguage("episodio\s(?<episodeNo>\d+)\sdella\sstagione\s(?<seasonNo>\d+)", "Italian"),
            New RegexLanguage("第\s(?<seasonNo>\d+)\s季第\s(?<episodeNo>\d+)\s集", "Chinese"),
            New RegexLanguage("시즌\s(?<seasonNo>\d+):\s(?<episodeNo>\d+)화", "Korean"),
            New RegexLanguage("(?<episodeNo>\d+)\sمن موسم\s(?<seasonNo>\d+)", "Arabic"),
            New RegexLanguage("odcinek\s(?<episodeNo>\d+)\ssezonu\s(?<seasonNo>\d+)", "Polish"),
            New RegexLanguage("(?<seasonNo>\d+)\.\ssezon\s(?<episodeNo>\d+)\.\sbölüm", "Turkish"),
            New RegexLanguage("ตอน\s(?<episodeNo>\d+)\sของซีซั่น\s(?<seasonNo>\d+)", "Thai"),
            New RegexLanguage("episodul\s(?<episodeNo>\d+)\sdin\ssezonul\s(?<seasonNo>\d+)", "Romanian"),
            New RegexLanguage("פרק\s(?<episodeNo>\d+)\sבעונה\s(?<seasonNo>\d+)", "Hebrew"),
            New RegexLanguage("Επεισόδιο\s(?<episodeNo>\d+)\sτης\sΣεζόν\s(?<seasonNo>\d+)", "Greek"),
            New RegexLanguage("episode\s(?<episodeNo>\d+)\sseason\s(?<seasonNo>\d+)", "Indonesian")
        }

        Private Class RegexLanguage
            Public Property Regex As Regex
            Public Property Language As String

            Public Sub New(regexPattern As String, language As String)
                Regex = New Regex(regexPattern, RegexOptions.IgnoreCase)
                Me.Language = language
            End Sub
        End Class

    End Class

End Namespace
