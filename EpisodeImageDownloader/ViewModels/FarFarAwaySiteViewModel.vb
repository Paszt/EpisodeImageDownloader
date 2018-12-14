Imports CsQuery
Imports System.Text.RegularExpressions
Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization.Json

Namespace ViewModels

    Public Class FarFarAwaySiteViewModel
        Inherits ViewModels.ViewModelBase

        Public Sub New()
            EpisodeInfos = New ObservableCollection(Of EpisodeInfo)
        End Sub

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

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _episodeInfos As ObservableCollection(Of EpisodeInfo)
        Public Property EpisodeInfos As ObservableCollection(Of EpisodeInfo)
            Get
                Return _episodeInfos
            End Get
            Set(value As ObservableCollection(Of EpisodeInfo))
                SetProperty(_episodeInfos, value)
            End Set
        End Property

        Public ReadOnly Property EpisodeDownloadFolder(EpisodeNumber As Integer) As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe,
                                       "Season " & CInt(SeasonNumber).ToString("00"),
                                       "S" & CInt(SeasonNumber).ToString("00") & "E" & CInt(EpisodeNumber).ToString("00"))
            End Get
        End Property

        Public ReadOnly Property SeasonDownloadFolder As String
            Get
                If SeasonNumber.HasValue AndAlso Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe,
                                           "Season " & CInt(SeasonNumber).ToString("00"))
                Else
                    Return String.Empty
                End If
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return EpisodeInfos.Count > 0 AndAlso
                   EpisodeInfoValid() AndAlso
                   Not String.IsNullOrEmpty(ShowName) AndAlso
                   SeasonNumber.HasValue
        End Function

        Private Function EpisodeInfoValid() As Boolean
            Return EpisodeInfos.Where(Function(e) Not String.IsNullOrWhiteSpace(e.EpisodeUrl)).Count() = EpisodeInfos.Count()
            'Dim returnValue As Boolean = True
            'For Each info In EpisodeInfos
            '    If info.EpisodeNumber.HasValue AndAlso Not String.IsNullOrEmpty(info.EpisodeUrl) Then
            '        Return True
            '    End If
            'Next
            'Return False
        End Function

        'Public Sub Test()
        '    DownloadImages()
        'End Sub

        Protected Overrides Sub DownloadImages()
            ''Parallel.ForEach(EpisodeInfos, New ParallelOptions() With {.MaxDegreeOfParallelism = 6}, AddressOf DownloadEpisodeImages)

            For Each EpisodeInfo In EpisodeInfos
                DownloadEpisodeImages(EpisodeInfo)
            Next
        End Sub

        Private Sub DownloadEpisodeImages(info As EpisodeInfo)
            If info.EpisodeNumber.HasValue Then
                Dim imageUris As New List(Of Uri)
                Dim html As String
                Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                    html = client.DownloadString(info.EpisodeUrl)
                End Using
                Dim cqDoc As CQ = CQ.Create(html)
                cqDoc("p a img").Each(Sub(domObj As IDomObject)
                                          ''Dim temp = domObj.Cq().Parent(0)
                                          Dim imgUri As Uri = New Uri(domObj.Cq().Parent.Attr("href"), UriKind.RelativeOrAbsolute)
                                          If Not imgUri.IsAbsoluteUri Then
                                              ' "." = current dir, like MS-DOS
                                              imgUri = New Uri(New Uri(New Uri(info.EpisodeUrl), "."), imgUri)
                                          End If
                                          imageUris.Add(imgUri)
                                      End Sub)

                Parallel.ForEach(imageUris, New ParallelOptions() With {.MaxDegreeOfParallelism = 6},
                                 Sub(imageUri As Uri)
                                     DownloadImage(imageUri, CInt(info.EpisodeNumber))
                                 End Sub)
                'For Each imgUri In imageUris
                '    DownloadImage(imgUri, CInt(info.EpisodeNumber))
                'Next
            End If
        End Sub

        Private Sub DownloadImage(ByRef imageUri As Uri, episodeNumber As Integer)
            Dim Filename = IO.Path.GetFileName(imageUri.AbsolutePath)
            Dim SeasonEpisodeFilename = "S" & SeasonNumber.Value.ToString("00") & "E" & episodeNumber.ToString("00") & "-" & Filename
            Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(episodeNumber), SeasonEpisodeFilename.Replace(" ", String.Empty))
            DownloadImageAddResult(imageUri.ToString(), localPath)
        End Sub

        Public Function SeasonDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(SeasonDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(SeasonDownloadFolder)
                                                       End Sub, AddressOf SeasonDownloadFolderExists)
            End Get
        End Property

#Region " Save/Load Episode Info "

        Public Function SeasonInfoFileName() As String
            If SeasonNumber.HasValue Then
                Return IO.Path.Combine(SeasonDownloadFolder, ShowName.MakeFileNameSafe & "-S" & SeasonNumber.Value.ToString("00") & ".eid")
            Else
                Return String.Empty
            End If
        End Function

        Public Function SeasonInfoFileExists() As Boolean
            Return IO.File.Exists(SeasonInfoFileName)
        End Function

        Public ReadOnly Property SaveSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(SeasonDownloadFolder) Then
                            IO.Directory.CreateDirectory(SeasonDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(SeasonInfoFileName) Then
                            System.IO.File.WriteAllText(SeasonInfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(SeasonInfoFileName)
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(FarFarAwaySiteViewModel))
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse
                               (Not String.IsNullOrWhiteSpace(ShowName) AndAlso SeasonNumber.HasValue AndAlso EpisodeInfos.Count > 0)
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Dim ffaVM As New FarFarAwaySiteViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(FarFarAwaySiteViewModel))
                            ffaVM = CType(jsonSerializer.ReadObject(stream), FarFarAwaySiteViewModel)
                        End Using
                        EpisodeInfos = ffaVM.EpisodeInfos
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse SeasonInfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

    End Class

End Namespace
