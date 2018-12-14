Public Class MessageWindow

    Private Sub New()
        InitializeComponent()
    End Sub

    Public Delegate Function ShowDialogDelegate(messageWindowText As String,
                                                caption As String,
                                                ShowCancel As Boolean) As Boolean?

    Public Overloads Shared Function ShowDialog(messageWindowText As String,
                                                caption As String,
                                                Optional ShowCancel As Boolean = False) As Boolean?
        If Windows.Application.Current IsNot Nothing Then
            Return CType(Windows.Application.Current.Dispatcher.Invoke(New ShowDialogDelegate(AddressOf ShowDialogInternal),
                                                                       New Object() {messageWindowText, caption, ShowCancel}),
                                                                       Boolean?)
        End If
    End Function

    Private Shared Function ShowDialogInternal(messageWindowText As String, caption As String, ShowCancel As Boolean) As Boolean?
        Dim mw As New MessageWindow() With {
            .Owner = Application.Current.MainWindow,
            .WindowStartupLocation = Windows.WindowStartupLocation.CenterOwner,
            .ShowStatusBar = False,
            .MinHeight = 200,
            .MinWidth = 450,
            .MaxWidth = 650,
            .SizeToContent = Windows.SizeToContent.WidthAndHeight,
            .ShowMinMax = False,
            .ShowInTaskbar = False,
            .Title = caption
        }
        mw.MessageTextBlock.Text = messageWindowText
        If ShowCancel Then
            mw.CancelButton.Visibility = Windows.Visibility.Visible
        Else
            mw.CancelButton.Visibility = Windows.Visibility.Collapsed
        End If
        If ShowCancel = True Then
            mw.CancelButton.Focus()
        Else
            mw.OkButton.Focus()
        End If
        Return mw.ShowDialog()
    End Function

    Private Overloads Function ShowDialog() As Boolean?
        Return MyBase.ShowDialog()
    End Function

    Private Overloads Sub Show()
    End Sub

    Private Sub OkButton_Click(sender As Object, e As RoutedEventArgs) Handles OkButton.Click
        Me.DialogResult = True
        Me.Close()
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As RoutedEventArgs) Handles CancelButton.Click
        Me.DialogResult = False
        Me.Close()
    End Sub

End Class
