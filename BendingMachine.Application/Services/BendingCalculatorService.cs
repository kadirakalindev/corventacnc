using BendingMachine.Application.DTOs;

namespace BendingMachine.Application.Services;

public class BendingCalculatorService
{
    public async Task<BendingCalculationResult> CalculateAsync(BendingParameters parameters)
    {
        try
        {
            // Adım 1: Temel değerlerin hesaplanması
            var bottomBallRadius = parameters.BottomBallDiameter / 2.0;
            var effectiveBendingRadius = parameters.BendingRadius + bottomBallRadius;

            // Adım 2: Üçgen yüksekliğinin hesaplanması
            var triangleAngleRadians = parameters.TriangleAngle * (Math.PI / 180.0);
            var triangleHeightCalculated = parameters.TriangleWidth / Math.Tan(triangleAngleRadians);

            // Adım 3: Yan top pozisyonlarının geometrik hesaplanması
            var radiusCenterPosition = effectiveBendingRadius - triangleHeightCalculated;
            var offsetDistanceCalculated = Math.Sin(triangleAngleRadians) * radiusCenterPosition;
            var tempAsinArgument = (-offsetDistanceCalculated) / effectiveBendingRadius;
            var alphaPrimeRadians = Math.Asin(tempAsinArgument);
            var totalAngleForTrigRadians = alphaPrimeRadians + triangleAngleRadians;

            // Adım 4: Yan topların nihai koordinatları
            var sideBallX = -Math.Sin(totalAngleForTrigRadians) * effectiveBendingRadius;
            var sideBallY = effectiveBendingRadius - (Math.Cos(totalAngleForTrigRadians) * effectiveBendingRadius);

            // Adım 5: Yan piston hareket mesafesinin hesaplanması
            var sideBallTravelDistance = Math.Sqrt(Math.Pow((parameters.TriangleWidth + sideBallX), 2) + Math.Pow(sideBallY, 2));

            // Adım 6: Top pozisyonlarının belirlenmesi
            var result = new BendingCalculationResult
            {
                // Hesaplama sonuçları
                EffectiveBendingRadius = effectiveBendingRadius,
                TriangleHeight = triangleHeightCalculated,
                SideBallTravelDistance = sideBallTravelDistance,
                
                // Büküm merkezi
                BendingCenterX = 0,
                BendingCenterY = radiusCenterPosition,
                CalculatedRadius = effectiveBendingRadius,
                
                // Üçgen boyutları
                TriangleWidth = parameters.TriangleWidth,
                
                // Top pozisyonları
                BottomBallPosition = new BallPosition { X = 0, Y = 0 },
                TopBallPosition = new BallPosition 
                { 
                    X = 0, 
                    Y = bottomBallRadius + parameters.ProfileHeight + bottomBallRadius 
                },
                LeftBallPosition = new BallPosition { X = sideBallX, Y = sideBallY },
                RightBallPosition = new BallPosition { X = -sideBallX, Y = sideBallY },
                
                // Adım hesaplamaları
                StepCount = (int)Math.Ceiling(sideBallTravelDistance / parameters.StepSize),
                StepSize = parameters.StepSize
            };

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Büküm hesaplama hatası: {ex.Message}", ex);
        }
    }
} 