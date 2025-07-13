using FluentValidation;
using BendingMachine.Application.DTOs;

namespace BendingMachine.Application.Validators;

public class BendingCalculationRequestValidator : AbstractValidator<BendingCalculationRequestDto>
{
    public BendingCalculationRequestValidator()
    {
        RuleFor(x => x.BottomBallDiameter)
            .GreaterThan(50)
            .LessThanOrEqualTo(1000)
            .WithMessage("Bottom ball diameter must be between 50-1000mm");

        RuleFor(x => x.BendingRadius)
            .GreaterThan(100)
            .LessThanOrEqualTo(5000)
            .WithMessage("Bending radius must be between 100-5000mm");

        RuleFor(x => x.ProfileHeight)
            .GreaterThan(10)
            .LessThanOrEqualTo(200)
            .WithMessage("Profile height must be between 10-200mm");

        RuleFor(x => x.TriangleWidth)
            .GreaterThan(100)
            .LessThanOrEqualTo(2000)
            .WithMessage("Triangle width must be between 100-2000mm");

        RuleFor(x => x.TriangleAngle)
            .GreaterThan(5)
            .LessThan(89)
            .WithMessage("Triangle angle must be between 5-89 degrees");

        RuleFor(x => x.StepSize)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(50)
            .WithMessage("Step size must be between 0.1-50mm");
    }
}

public class BendingParametersValidator : AbstractValidator<BendingParametersDto>
{
    public BendingParametersValidator()
    {
        RuleFor(x => x.BendingRadius)
            .GreaterThan(100)
            .LessThanOrEqualTo(5000)
            .WithMessage("Bending radius must be between 100-5000mm");

        RuleFor(x => x.ProfileHeight)
            .GreaterThan(10)
            .LessThanOrEqualTo(200)
            .WithMessage("Profile height must be between 10-200mm");

        RuleFor(x => x.TriangleWidth)
            .GreaterThan(100)
            .LessThanOrEqualTo(2000)
            .WithMessage("Triangle width must be between 100-2000mm");

        RuleFor(x => x.TriangleAngle)
            .GreaterThan(5)
            .LessThan(89)
            .WithMessage("Triangle angle must be between 5-89 degrees");

        RuleFor(x => x.StepSize)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(50)
            .WithMessage("Step size must be between 0.1-50mm");

        RuleFor(x => x.TargetPressure)
            .GreaterThan(10)
            .LessThanOrEqualTo(400)
            .WithMessage("Target pressure must be between 10-400 bar");

        RuleFor(x => x.PressureTolerance)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(10)
            .WithMessage("Pressure tolerance must be between 0.1-10 bar");

        RuleFor(x => x.ResetDistance)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(1000)
            .WithMessage("Reset distance must be between 0.1-1000mm");
    }
}

public class BendingStepValidator : AbstractValidator<BendingStepDto>
{
    public BendingStepValidator()
    {
        RuleFor(x => x.StepNumber)
            .GreaterThan(0)
            .WithMessage("Step number must be greater than 0");

        RuleFor(x => x.LeftPosition)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(422)
            .WithMessage("Left position must be between 0-422mm");

        RuleFor(x => x.RightPosition)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(422)
            .WithMessage("Right position must be between 0-422mm");

        RuleFor(x => x.RotationAngle)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(360)
            .WithMessage("Rotation angle must be between 0-360 degrees");

        RuleFor(x => x.RotationSpeed)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(100)
            .WithMessage("Rotation speed must be between 0.1-100");

        RuleFor(x => x.RotationDuration)
            .GreaterThan(0.1)
            .LessThanOrEqualTo(60)
            .WithMessage("Rotation duration must be between 0.1-60 seconds");

        RuleFor(x => x.RotationDirection)
            .Must(x => x == "Clockwise" || x == "Counterclockwise")
            .WithMessage("Rotation direction must be 'Clockwise' or 'Counterclockwise'");
    }
} 