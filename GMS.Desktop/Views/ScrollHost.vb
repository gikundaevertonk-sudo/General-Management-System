Imports System.Drawing
Imports System.Windows.Forms

Namespace Views

    ''' <summary>
    ''' The one scrollable surface for a page made of stacked sections: a single vertical scrollbar
    ''' at the right-hand edge, top to bottom, instead of a scrollbar per strip.
    ''' </summary>
    ''' <remarks>
    ''' Sections dock to the top at their natural height and must not scroll themselves. A grid
    ''' sized to its rows has no scrollbar to consume the mouse wheel, so it swallows the wheel
    ''' without moving the page; <see cref="ForwardWheel"/> hands it back.
    ''' </remarks>
    Friend NotInheritable Class ScrollHost
        Inherits Panel

        Public Sub New()
            AutoScroll = True
            Dock = DockStyle.Fill
            DoubleBuffered = True
        End Sub

        ''' <summary>Makes wheel movement over <paramref name="child"/> scroll this page.</summary>
        Public Sub ForwardWheel(child As Control)
            AddHandler child.MouseWheel,
                Sub(s, e)
                    ' AutoScrollPosition reads back negative, but is set with positive numbers.
                    AutoScrollPosition = New Point(0, -AutoScrollPosition.Y - e.Delta)
                    If TypeOf e Is HandledMouseEventArgs Then DirectCast(e, HandledMouseEventArgs).Handled = True
                End Sub
        End Sub
    End Class

End Namespace
