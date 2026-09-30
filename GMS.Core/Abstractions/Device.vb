Namespace Abstractions

    ''' <summary>
    ''' The installation this process is running on, for a client that can work offline.
    ''' </summary>
    ''' <remarks>
    ''' Registered only by the desktop client. Services that number documents take it as optional:
    ''' without it (GMS.Web, tests) they number exactly as they always have.
    ''' </remarks>
    Public Interface IDeviceIdentity
        ''' <summary>
        ''' A short code unique to this installation, e.g. "K7Q2". Put into document numbers made
        ''' here, so two computers numbering sales while offline cannot both issue the same one.
        ''' </summary>
        ReadOnly Property DeviceCode As String
    End Interface

End Namespace
