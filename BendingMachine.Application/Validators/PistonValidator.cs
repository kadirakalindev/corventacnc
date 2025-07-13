using FluentValidation;
using BendingMachine.Application.DTOs;

namespace BendingMachine.Application.Validators;

public class PistonMoveRequestValidator : AbstractValidator<PistonMoveRequestDto>
{
    public PistonMoveRequestValidator()
    {
        RuleFor(x => x.PistonType)
            .NotEmpty()
            .WithMessage("Piston type is required")
            .Must(x => IsValidPistonType(x))
            .WithMessage("Invalid piston type");

        RuleFor(x => x.Voltage)
            .GreaterThanOrEqualTo(-10)
            .LessThanOrEqualTo(10)
            .WithMessage("Voltage must be between -10V and +10V");
    }

    private static bool IsValidPistonType(string pistonType)
    {
        var validTypes = new[] { "TopPiston", "BottomPiston", "LeftPiston", "RightPiston",
                                "LeftReelPiston", "RightReelPiston", "LeftBodyPiston", 
                                "RightBodyPiston", "LeftJoinPiston", "RightJoinPiston" };
        return validTypes.Contains(pistonType);
    }
}

public class PistonPositionRequestValidator : AbstractValidator<PistonPositionRequestDto>
{
    public PistonPositionRequestValidator()
    {
        RuleFor(x => x.PistonType)
            .NotEmpty()
            .WithMessage("Piston type is required")
            .Must(x => IsValidPistonType(x))
            .WithMessage("Invalid piston type");

        RuleFor(x => x.TargetPosition)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Target position must be greater than or equal to 0mm");

        RuleFor(x => x.Speed)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(10)
            .WithMessage("Speed must be between 0.1-10 V");

        // Position range validation based on piston type
        RuleFor(x => x)
            .Must(x => ValidatePositionForPistonType(x.PistonType, x.TargetPosition))
            .WithMessage("Target position is out of range for this piston type");
    }

    private static bool IsValidPistonType(string pistonType)
    {
        var validTypes = new[] { "TopPiston", "BottomPiston", "LeftPiston", "RightPiston",
                                "LeftReelPiston", "RightReelPiston", "LeftBodyPiston", 
                                "RightBodyPiston", "LeftJoinPiston", "RightJoinPiston" };
        return validTypes.Contains(pistonType);
    }

    private static bool ValidatePositionForPistonType(string pistonType, double position)
    {
        return pistonType switch
        {
            "TopPiston" => position <= 160.0,
            "BottomPiston" => position <= 195.0,
            "LeftPiston" or "RightPiston" => position <= 422.0,
            "LeftReelPiston" or "RightReelPiston" => position <= 352.0,
            "LeftBodyPiston" or "RightBodyPiston" => position <= 129.0,
            "LeftJoinPiston" or "RightJoinPiston" => position <= 187.0,
            _ => false
        };
    }
}

public class PistonJogRequestValidator : AbstractValidator<PistonJogRequestDto>
{
    public PistonJogRequestValidator()
    {
        RuleFor(x => x.PistonType)
            .NotEmpty()
            .WithMessage("Piston type is required")
            .Must(x => IsValidPistonType(x))
            .WithMessage("Invalid piston type");

        RuleFor(x => x.Direction)
            .NotEmpty()
            .WithMessage("Direction is required")
            .Must(x => x == "Forward" || x == "Backward")
            .WithMessage("Direction must be 'Forward' or 'Backward'");

        RuleFor(x => x.Voltage)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(10)
            .WithMessage("Voltage must be between 0.1-10V for jogging");
    }

    private static bool IsValidPistonType(string pistonType)
    {
        var validTypes = new[] { "TopPiston", "BottomPiston", "LeftPiston", "RightPiston",
                                "LeftReelPiston", "RightReelPiston", "LeftBodyPiston", 
                                "RightBodyPiston", "LeftJoinPiston", "RightJoinPiston" };
        return validTypes.Contains(pistonType);
    }
} 