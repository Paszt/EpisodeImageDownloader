Imports System.Collections.ObjectModel
Imports System.ComponentModel

Namespace ViewModels

    Public Class UploadViewModel
        Implements INotifyPropertyChanged

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

        Private _showId As Integer?
        Public Property ShowId As Integer?
            Get
                Return _showId
            End Get
            Set(value As Integer?)
                SetProperty(_showId, value)
            End Set
        End Property

        Private _episodeImageResults As ObservableCollection(Of EpisodeImageResult)
        Public Property EpisodeImageResults As ObservableCollection(Of EpisodeImageResult)
            Get
                Return _episodeImageResults
            End Get
            Set(value As ObservableCollection(Of EpisodeImageResult))
                SetProperty(_episodeImageResults, value)
            End Set
        End Property

        Private _filesToUpload As ObservableCollection(Of String)
        Public Property FilesToUpload As ObservableCollection(Of String)
            Get
                Return _filesToUpload
            End Get
            Set(value As ObservableCollection(Of String))
                SetProperty(_filesToUpload, value)
            End Set
        End Property

        Private _foldersToUpload As ObservableCollection(Of String)
        Public Property FoldersToUpload As ObservableCollection(Of String)
            Get
                Return _foldersToUpload
            End Get
            Set(value As ObservableCollection(Of String))
                SetProperty(_foldersToUpload, value)
            End Set
        End Property

#End Region

#Region " INotifyPropertyChanged Members "

        Public Event PropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs) Implements ComponentModel.INotifyPropertyChanged.PropertyChanged

        Protected Function SetProperty(Of T)(ByRef storage As T, value As T,
                                             <Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing) As Boolean
            If Equals(storage, value) Then
                Return False
            End If
            storage = value
            Me.OnPropertyChanged(propertyName)
            Return True
        End Function

        Protected Sub OnPropertyChanged(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
            Dim propertyChanged As System.ComponentModel.PropertyChangedEventHandler = Me.PropertyChangedEvent
            If propertyChanged IsNot Nothing Then
                propertyChanged(Me, New System.ComponentModel.PropertyChangedEventArgs(propertyName))
            End If
        End Sub

#End Region

    End Class

End Namespace
