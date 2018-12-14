Imports System.Threading
Imports System.Collections.Concurrent

Namespace Infrastructure

    ''' <summary>
    ''' Used to control the rate of some occurrence per unit of time.
    ''' </summary>
    ''' <remarks>
    '''     <para>
    '''     To control the rate of an action using a <see cref="RateGate"/>, 
    '''     code should simply call WaitToProceed() prior to 
    '''     performing the action. WaitToProceed() will block
    '''     the current thread until the action is allowed based on the rate 
    '''     limit.
    '''     </para>
    '''     <para>
    '''     This class is thread safe. A single <see cref="RateGate"/> instance 
    '''     may be used to control the rate of an occurrence across multiple 
    '''     threads.
    '''     </para>
    ''' </remarks>
    Public Class RateGate
        Implements IDisposable

        ' Semaphore used to count and limit the number of occurrences per
        ' unit time.
        Private ReadOnly _semaphore As SemaphoreSlim

        ' Times (in millisecond ticks) at which the semaphore should be exited.
        Private ReadOnly _exitTimes As ConcurrentQueue(Of Integer)

        ' Timer used to trigger exiting the semaphore.
        Private ReadOnly _exitTimer As Timer

        ''' <summary>
        ''' Number of occurrences allowed per unit of time.
        ''' </summary>
        Public Property Occurrences() As Integer
            Get
                Return m_Occurrences
            End Get
            Private Set(value As Integer)
                m_Occurrences = value
            End Set
        End Property
        Private m_Occurrences As Integer

        ''' <summary>
        ''' The length of the time unit, in milliseconds.
        ''' </summary>
        Public Property TimeUnitMilliseconds() As Integer
            Get
                Return m_TimeUnitMilliseconds
            End Get
            Private Set(value As Integer)
                m_TimeUnitMilliseconds = value
            End Set
        End Property
        Private m_TimeUnitMilliseconds As Integer

        ''' <summary>
        ''' Initializes a <see cref="RateGate"/> with a rate of <paramref name="NumberOfOccurrences"/> 
        ''' per <paramref name="timeUnit"/>.
        ''' </summary>
        ''' <param name="NumberOfOccurrences">Number of occurrences allowed per unit of time.</param>
        ''' <param name="timeUnit">Length of the time unit.</param>
        ''' <exception cref="ArgumentOutOfRangeException">
        ''' If <paramref name="NumberOfOccurrences"/> or <paramref name="timeUnit"/> is negative.
        ''' </exception>
        Public Sub New(NumberOfOccurrences As Integer, timeUnit As TimeSpan)
            ' Check the arguments.
            If NumberOfOccurrences <= 0 Then
                Throw New ArgumentOutOfRangeException("occurrences", "Number of occurrences must be a positive integer")
            End If
            If timeUnit <> timeUnit.Duration() Then
                Throw New ArgumentOutOfRangeException("timeUnit", "Time unit must be a positive span of time")
            End If
            If timeUnit >= TimeSpan.FromMilliseconds(UInt32.MaxValue) Then
                Throw New ArgumentOutOfRangeException("timeUnit", "Time unit must be less than 2^32 milliseconds")
            End If

            Occurrences = NumberOfOccurrences
            TimeUnitMilliseconds = CInt(timeUnit.TotalMilliseconds)

            ' Create the semaphore, with the number of occurrences as the maximum count.
            _semaphore = New SemaphoreSlim(Occurrences, Occurrences)

            ' Create a queue to hold the semaphore exit times.
            _exitTimes = New ConcurrentQueue(Of Integer)()

            ' Create a timer to exit the semaphore. Use the time unit as the original
            ' interval length because that's the earliest we will need to exit the semaphore.
            _exitTimer = New Timer(AddressOf ExitTimerCallback, Nothing, TimeUnitMilliseconds, -1)
        End Sub

        ' Callback for the exit timer that exits the semaphore based on exit times 
        ' in the queue and then sets the timer for the nextexit time.
        Private Sub ExitTimerCallback(state As Object)
            ' While there are exit times that are passed due still in the queue,
            ' exit the semaphore and dequeue the exit time.
            Dim exitTime As Integer
            While _exitTimes.TryPeek(exitTime) AndAlso exitTime - Environment.TickCount <= 0
                If disposedValue Then Return
                _semaphore.Release()
                _exitTimes.TryDequeue(exitTime)
            End While

            ' Try to get the next exit time from the queue and compute
            ' the time until the next check should take place. If the 
            ' queue is empty, then no exit times will occur until at least
            ' one time unit has passed.
            Dim timeUntilNextCheck As Integer
            If _exitTimes.TryPeek(exitTime) Then
                timeUntilNextCheck = exitTime - Environment.TickCount
            Else
                timeUntilNextCheck = TimeUnitMilliseconds
            End If

            ' Set the timer.
            _exitTimer.Change(timeUntilNextCheck, -1)
        End Sub

        ''' <summary>
        ''' Blocks the current thread until allowed to proceed or until the
        ''' specified timeout elapses.
        ''' </summary>
        ''' <param name="millisecondsTimeout">Number of milliseconds to wait, or -1 to wait indefinitely.</param>
        ''' <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        Public Function WaitToProceed(millisecondsTimeout As Integer) As Boolean
            ' Check the arguments.
            If millisecondsTimeout < -1 Then
                Throw New ArgumentOutOfRangeException("millisecondsTimeout")
            End If

            CheckDisposed()

            ' Block until we can enter the semaphore or until the timeout expires.
            Dim entered = _semaphore.Wait(millisecondsTimeout)

            ' If we entered the semaphore, compute the corresponding exit time 
            ' and add it to the queue.
            If entered Then
                Dim timeToExit = Environment.TickCount + TimeUnitMilliseconds
                _exitTimes.Enqueue(timeToExit)
            End If

            Return entered
        End Function

        ''' <summary>
        ''' Blocks the current thread until allowed to proceed or until the
        ''' specified timeout elapses.
        ''' </summary>
        ''' <param name="timeout"></param>
        ''' <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        Public Function WaitToProceed(timeout As TimeSpan) As Boolean
            Return WaitToProceed(CInt(timeout.TotalMilliseconds))
        End Function

        ''' <summary>
        ''' Blocks the current thread indefinitely until allowed to proceed.
        ''' </summary>
        Public Sub WaitToProceed()
            WaitToProceed(Timeout.Infinite)
        End Sub

#Region "IDisposable Support"

        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TO DO: dispose managed state (managed objects).
                    _semaphore.Dispose()

                    _exitTimer.Dispose()
                End If

                ' TO DO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TO DO: set large fields to null.
            End If
            Me.disposedValue = True
        End Sub

        ' TO DO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        ' Throws an ObjectDisposedException if this object is disposed.
        Private Sub CheckDisposed()
            If disposedValue Then
                Throw New ObjectDisposedException("RateGate is already disposed")
            End If
        End Sub

#End Region

    End Class

    Public Module EnumerableExtensions

        ''' <summary>
        ''' Limits the rate at which the sequence is enumerated.
        ''' </summary>
        ''' <typeparam name="T">The type of the elements of <paramref name="source"/>.</typeparam>
        ''' <param name="source">The <see cref="IEnumerable(Of T)"/> whose enumeration is to be rate limited.</param>
        ''' <param name="count">The number of items in the sequence that are allowed to be processed per time unit.</param>
        ''' <param name="timeUnit">Length of the time unit.</param>
        ''' <returns>An <see cref="IEnumerable(Of T)"/> containing the elements of the source sequence.</returns>
        <System.Runtime.CompilerServices.Extension>
        Public Iterator Function LimitRate(Of T)(source As IEnumerable(Of T), count As Integer, timeUnit As TimeSpan) As IEnumerable(Of T)
            Using rateGate = New RateGate(count, timeUnit)
                For Each item In source
                    rateGate.WaitToProceed()
                    Yield item
                Next
            End Using
        End Function

    End Module

End Namespace
