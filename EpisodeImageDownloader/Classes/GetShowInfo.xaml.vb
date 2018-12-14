Option Strict On

Public Class GetShowInfo

    Public Property ShowName As String
        Get
            Return ShowNameTextBox.Text
        End Get
        Set(value As String)
            ShowNameTextBox.Text = value
        End Set
    End Property

    Public Property SeasonNumber As Integer
        Get
            If IsNumeric(SeasonNumberTextBox.Text) Then
                Return CInt(SeasonNumberTextBox.Text)
            End If
            Return 0
        End Get
        Set(value As Integer)
            SeasonNumberTextBox.Text = value.ToString()
        End Set
    End Property

    Public Property Data As String
        Get
            Return DataTextBox.Text
        End Get
        Set(value As String)
            DataTextBox.Text = value
        End Set
    End Property

    Private Sub OkButton_Click(sender As Object, e As RoutedEventArgs) Handles OkButton.Click
        Me.DialogResult = True
        Me.Hide()
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As RoutedEventArgs) Handles CancelButton.Click
        Me.DialogResult = False
        Me.Hide()
    End Sub

End Class
