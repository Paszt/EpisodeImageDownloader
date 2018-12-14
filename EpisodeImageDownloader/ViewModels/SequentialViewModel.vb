Option Strict On

Namespace ViewModels

    Public Class SequentialViewModel
        Inherits ViewModelBase

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

        Private _seasonNumber As Integer? = 1
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _urlFormat As String
        Public Property UrlFormat As String
            Get
                Return _urlFormat
            End Get
            Set(value As String)
                SetProperty(_urlFormat, value)
            End Set
        End Property

        Private _imageNumberLength As Integer = 2
        ''' <summary>
        ''' The length of the image number, ImageFrom and ImageTo integers will
        '''   be padded with leading zeros to match this length
        ''' </summary>
        Public Property ImageNumberLength As Integer
            Get
                Return _imageNumberLength
            End Get
            Set(value As Integer)
                SetProperty(_imageNumberLength, value)
            End Set
        End Property

        Private _imageFrom As Integer?
        Public Property ImageFrom As Integer?
            Get
                Return _imageFrom
            End Get
            Set(value As Integer?)
                SetProperty(_imageFrom, value)
            End Set
        End Property

        Private _imageTo As Integer?
        Public Property ImageTo As Integer?
            Get
                Return _imageTo
            End Get
            Set(value As Integer?)
                SetProperty(_imageTo, value)
            End Set
        End Property

        Private _useOriginalFilename As Boolean
        Public Property UseOriginalFilename As Boolean
            Get
                Return _useOriginalFilename
            End Get
            Set(value As Boolean)
                SetProperty(_useOriginalFilename, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                SeasonNumber.HasValue AndAlso
                ImageFrom.HasValue AndAlso
                ImageTo.HasValue
        End Function

        Protected Overrides Sub DownloadImages()
            Dim seasonFolder = "Season " & SeasonNumber.Value.ToString("D2")
            Dim seasonFolderPath = IO.Path.Combine(ShowDownloadFolder, seasonFolder)
            'For i = ImageFrom.Value To ImageTo.Value
            '    Dim iStr As String = i.ToString("D" & ImageNumberLength)
            '    Dim imageUrl = String.Format(UrlFormat, iStr)
            '    Dim localFileName As String = "S" & SeasonNumber.Value.ToString("D2") & "E" & iStr & IO.Path.GetExtension(imageUrl)
            '    Dim localFilePath = IO.Path.Combine(seasonFolderPath, localFileName)
            '    MyBase.DownloadImageAddResult(imageUrl, localFilePath)
            'Next
            Parallel.For(ImageFrom.Value, ImageTo.Value + 1,
                         Sub(imageNumber)
                             Dim iStr As String = imageNumber.ToString("D" & ImageNumberLength)
                             Dim imageUrl = String.Format(UrlFormat, iStr)
                             Dim localFileName As String
                             If UseOriginalFilename Then
                                 localFileName = IO.Path.GetFileName(imageUrl)
                             Else
                                 localFileName = "S" & SeasonNumber.Value.ToString("D2") & "E" & iStr & IO.Path.GetExtension(imageUrl)
                             End If
                             Dim localFilePath = IO.Path.Combine(seasonFolderPath, localFileName)
                             DownloadImageAddResult(imageUrl, localFilePath)
                         End Sub)
        End Sub

#Region " Folder Functions "

        Public Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe())
            Else
                Return String.Empty
            End If
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder())
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder())
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

#End Region

    End Class

End Namespace
