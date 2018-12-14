Imports System.Net
Imports System.Net.Sockets

Namespace Infrastructure

    Public Class ChromeWebClient
        Inherits WebClient

        Private _request As HttpWebRequest

        Public Property AllowAutoRedirect As Boolean = False

        Public Property Referer As String

        Public Property Accept As String

        Public Sub New()
            MyBase.New()
            Encoding = Text.Encoding.UTF8
        End Sub

        Protected Overrides Function GetWebRequest(address As Uri) As WebRequest
            _request = CType(MyBase.GetWebRequest(address), HttpWebRequest)
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls Or
                SecurityProtocolType.Tls11 Or
                SecurityProtocolType.Tls12 Or
                SecurityProtocolType.Ssl3
            If _request IsNot Nothing Then
                '_request.UserAgent = ("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/38.0.2125.111 Safari/537.36")
                '_request.UserAgent = ("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.99 Safari/537.36")
                '_request.UserAgent = ("Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36")
                '_request.UserAgent = ("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36")
                _request.UserAgent = ("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36")
                _request.AllowAutoRedirect = AllowAutoRedirect
                If Not String.IsNullOrWhiteSpace(Referer) Then
                    _request.Referer = Referer
                End If
                If Not String.IsNullOrWhiteSpace(Accept) Then
                    _request.Accept = Accept
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

        Public Shadows Function DownloadString(address As String) As String
            Return PerformRemoteAction(AddressOf MyBase.DownloadString, address)
        End Function

        Public Shadows Function DownloadString(uri As Uri) As String
            Return PerformRemoteAction(AddressOf MyBase.DownloadString, uri)
        End Function

        Public Shadows Sub DownloadFile(address As String, fileName As String)
            Dim currentRetry As Integer = 0
            While True
                Try
                    ' Calling external service.
                    MyBase.DownloadFile(address, fileName)
                    Return
                Catch ex As Exception
                    Trace.TraceError("Operation Exception")
                    currentRetry += 1
                    If currentRetry > 2 OrElse Not IsExceptionTransient(ex) Then
                        ' If this is not a transient error or retry count has
                        ' been reached rethrow the exception.
                        Throw
                    End If
                End Try
                ' Wait to retry the operation.
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1))
            End While
        End Sub

#Region " Retry on Transient Errors "

        ''' <summary>
        ''' Performs a method with a single string parameter which communicates with a network resource.
        ''' If the method fails with a transient type error, the action is retried 2 times, pausing 1 second
        ''' between tries.
        ''' </summary>
        ''' <typeparam name="T">The type of the return value.</typeparam>
        ''' <param name="Action">The method to perform.</param>
        ''' <param name="ActionParameter">The method's <see cref="System.String"/> parameter.</param>
        ''' <returns>The return value of the specified method.</returns>
        Public Shared Function PerformRemoteAction(Of T)(Action As Func(Of String, T),
                                                         ActionParameter As String) As T
            Dim currentRetry As Integer = 0
            While True
                Try
                    ' Calling external service.
                    Return Action(ActionParameter)
                Catch ex As Exception
                    Trace.TraceError("Operation Exception")
                    currentRetry += 1
                    If currentRetry > 2 OrElse Not IsExceptionTransient(ex) Then
                        ' If this is not a transient error or retry count has
                        ' been reached rethrow the exception.
                        Throw
                    End If
                End Try
                ' Wait to retry the operation.
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1))
            End While
        End Function

        Public Shared Function PerformRemoteAction(Of T)(Action As Func(Of Uri, T),
                                                         ActionParameter As Uri) As T
            Dim currentRetry As Integer = 0
            While True
                Try
                    ' Calling external service.
                    Return Action(ActionParameter)
                Catch ex As Exception
                    Trace.TraceError("Operation Exception")
                    currentRetry += 1
                    If currentRetry > 2 OrElse Not IsExceptionTransient(ex) Then
                        ' If this is not a transient error or retry count has
                        ' been reached rethrow the exception.
                        Throw
                    End If
                End Try
                ' Wait to retry the operation.
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1))
            End While
        End Function

#Region " Transient "

        Private Shared ReadOnly TransientHttpStatusCodes As Integer() = New Integer() {500, 504, 503, 408}
        Private Shared ReadOnly TransientWebExceptionStatuses As WebExceptionStatus() =
            New WebExceptionStatus() {WebExceptionStatus.ConnectionClosed,
                                      WebExceptionStatus.Timeout,
                                      WebExceptionStatus.RequestCanceled,
                                      WebExceptionStatus.KeepAliveFailure,
                                      WebExceptionStatus.PipelineFailure,
                                      WebExceptionStatus.ReceiveFailure,
                                      WebExceptionStatus.ConnectFailure,
                                      WebExceptionStatus.SendFailure}
        Private Shared ReadOnly TransientSocketErrorCodes As SocketError() = New SocketError() {SocketError.ConnectionRefused,
                                                                                                SocketError.TimedOut}

        ''' <summary>
        ''' Determines if the exception is a transient type exception.
        ''' </summary>
        ''' <param name="Exc">The exception to check.</param>
        ''' <returns>True if the exception is determined to be transient, false otherwise.</returns>
        Public Shared Function IsExceptionTransient(Exc As Exception) As Boolean
            Dim webExc = TryCast(Exc, WebException)
            If webExc IsNot Nothing Then
                If TransientWebExceptionStatuses.Contains(webExc.Status) Then
                    Return True
                End If
                If webExc.Status = WebExceptionStatus.ProtocolError Then
                    Dim httpWebResp As HttpWebResponse = TryCast(webExc.Response, HttpWebResponse)
                    If httpWebResp IsNot Nothing AndAlso
                        TransientHttpStatusCodes.Contains(CInt(httpWebResp.StatusCode)) Then
                        Return True
                    End If
                End If
                Return False
            Else
                Dim socketExc = TryCast(Exc, SocketException)
                If socketExc IsNot Nothing AndAlso TransientSocketErrorCodes.Contains(socketExc.SocketErrorCode) Then
                    Return True
                End If
            End If
            Return False
        End Function

#End Region

#End Region

    End Class

    Class WebResources

        Public Overloads Shared Function DownloadString(address As String) As String
            Using _client As New ChromeWebClient() With {.AllowAutoRedirect = True}
                Return _client.DownloadString(address)
            End Using
        End Function

        Public Overloads Shared Function DownloadString(address As Uri) As String
            Using _client As New ChromeWebClient() With {.AllowAutoRedirect = True}
                Return _client.DownloadString(address)
            End Using
        End Function

        Public Shared Sub DownloadFile(address As String, fileName As String)
            Using _client As New ChromeWebClient() With {.AllowAutoRedirect = True}
                _client.DownloadFile(address, fileName)
            End Using
        End Sub

    End Class

End Namespace
