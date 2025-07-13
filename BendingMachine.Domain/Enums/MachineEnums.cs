namespace BendingMachine.Domain.Enums;

public enum MotionEnum
{
    Closed = 0,
    Forward = 1,
    Backward = 2
}

public enum PistonType
{
    TopPiston = 1,
    BottomPiston = 2,
    LeftPiston = 3,
    RightPiston = 4,
    LeftReelPiston = 5,
    RightReelPiston = 6,
    LeftBodyPiston = 7,
    RightBodyPiston = 8,
    LeftJoinPiston = 9,
    RightJoinPiston = 10,
    
    // Aliases for MachineDriver compatibility
    TopCenter = TopPiston,
    BottomCenter = BottomPiston,
    LeftSide = LeftPiston,
    RightSide = RightPiston
}

public enum SafetyStatus
{
    Normal = 0,
    Safe = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public enum ValveGroup
{
    S1 = 1,
    S2 = 2
}

public enum RotationDirection
{
    Stopped = 0,
    Clockwise = 1,
    CounterClockwise = 2
}

public enum ConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Error = 3
} 