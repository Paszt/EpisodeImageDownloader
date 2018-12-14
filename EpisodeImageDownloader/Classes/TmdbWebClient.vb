Imports System.Net

Public Class TmdbWebClient
    Inherits WebClient

    Private _request As HttpWebRequest
    Private _cookieContainer As CookieContainer
    Private _contentType As String
    Private _uploadingImage As Boolean = False
    Private _preparingUpload As Boolean = False

    Public Property AllowAutoRedirect As Boolean = True
    Public Property Referer As String

    Public Sub New()
        MyBase.New()
        Me.Encoding = System.Text.Encoding.UTF8
        _cookieContainer = New CookieContainer()
        Login()

        Test()
    End Sub

    Protected Overrides Function GetWebRequest(address As Uri) As WebRequest
        _request = CType(MyBase.GetWebRequest(address), HttpWebRequest)
        If _request IsNot Nothing Then
            '_request.UserAgent = ("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/38.0.2125.111 Safari/537.36")
            _request.UserAgent = ("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.99 Safari/537.36")
            _request.AllowAutoRedirect = AllowAutoRedirect
            _request.CookieContainer = _cookieContainer
            _request.Headers.Add("Accept-Language: en-US,en;q=0.8")
            If _contentType IsNot Nothing Then
                _request.ContentType = _contentType
            End If
            If Referer IsNot Nothing Then
                _request.Referer = Referer
            End If
            If _uploadingImage Then
                _request.Accept = "*/*"
                _request.Headers.Add("Accept-Encoding: gzip, deflate")
                _request.KeepAlive = True
                _request.Headers.Add("Origin", "https://www.themoviedb.org")
            End If
            If _preparingUpload Then
                _request.Accept = "text/html, */*; q=0.01"
                _request.Headers.Add("Accept-Encoding: gzip, deflate, sdch")
                _request.Headers.Add("X-Requested-With: XMLHttpRequest")
                _request.KeepAlive = True
            End If
        End If
        Return _request
    End Function

    Public ReadOnly Property HttpStatusCode As HttpStatusCode
        Get
            If _request Is Nothing Then
                Return Nothing
            End If
            Dim response As HttpWebResponse = TryCast(MyBase.GetWebResponse(Me._request), HttpWebResponse)
            If response IsNot Nothing Then
                Return response.StatusCode
            Else
                Return Nothing
            End If
        End Get
    End Property

    Private Sub Login()
        Try
            Me.DownloadString("https://www.themoviedb.org")
            Me.Referer = "https://www.themoviedb.org/login"
            Me.UploadData("https://www.themoviedb.org/login", Text.Encoding.ASCII.GetBytes("username=Paszt&password=vass3r77"))
        Catch ex As Exception
            MessageWindow.ShowDialog(ex.Message, "Error Logging In")
        End Try
        Me.Referer = Nothing
    End Sub

    Private Sub Test()
        Try
            Dim boundary = "------WebKitFormBoundary" & DateTime.Now.Ticks.ToString("x")
            Dim header = boundary & Environment.NewLine
            header &= "Content-Disposition: form-data; name=""upload_files""; filename=""S16E01-If_I_Needed_Someone_1920x1080.jpg""" & Environment.NewLine
            header &= "Content-Type: image/jpeg" & Environment.NewLine & Environment.NewLine
            Dim headerData As Byte() = Text.Encoding.ASCII.GetBytes(header)
            Dim imageData = IO.File.ReadAllBytes("C:\Users\Stephen\Downloads\__TMDB\Holby City\Season 16\S16E01-If_I_Needed_Someone_1920x1080.jpg")
            Dim endBoundaryData = Text.Encoding.ASCII.GetBytes(Environment.NewLine & boundary & "--")
            'data = data.Concat(imagedata.ToArray)
            Dim postData As Byte() = New Byte(headerData.Length + imageData.Length + endBoundaryData.Length) {}
            Buffer.BlockCopy(headerData, 0, postData, 0, headerData.Length)
            Buffer.BlockCopy(imageData, 0, postData, headerData.Length, imageData.Length)
            Buffer.BlockCopy(endBoundaryData, 0, postData, headerData.Length + imageData.Length, endBoundaryData.Length)

            _contentType = "multipart/form-data; boundary=" & boundary
            'Me.UploadData("https://www.themoviedb.org/tv/1028-holby-city/season/16/episode/1/image?type=backdrop&translate=false", postData)
            Me.UploadImage(1028, "Holby City", 16, 1, postData)
        Catch ex As Exception
            MessageWindow.ShowDialog(ex.Message, "Error uploading image")
        End Try
        _contentType = Nothing
    End Sub

    Public Function UploadImage(showId As Integer, showName As String, seasonNumber As Integer, episodeNumber As Integer, data() As Byte) As Byte()
        Dim uploadUrl As String = "https://www.themoviedb.org/tv/" & showId.ToString() & "-" & HyphenatedShowName(showName)
        uploadUrl &= "/season/" & seasonNumber.ToString()
        uploadUrl &= "/episode/" & episodeNumber.ToString()
        Referer = uploadUrl
        Dim prepareUrl = uploadUrl & "/remote/upload_image?type=backdrop&translate=false&language=en"
        uploadUrl &= "/image?type=backdrop&translate=false"
        _preparingUpload = True
        Me.DownloadString(prepareUrl)
        _preparingUpload = False
        _uploadingImage = True
        Dim retValue = Me.UploadData(uploadUrl, data)
        _uploadingImage = False
        Return retValue
    End Function

    Private Function HyphenatedShowName(showName As String) As String
        Return showName.ToLower().Replace(" ", "-")
    End Function

End Class
