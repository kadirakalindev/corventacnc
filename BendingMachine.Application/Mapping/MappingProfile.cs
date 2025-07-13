using AutoMapper;
using BendingMachine.Application.DTOs;
using BendingMachine.Domain.Entities;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<MachineStatus, MachineStatusDto>()
            .ForMember(dest => dest.IsConnected, opt => opt.Ignore()); // Will be set by service
            
        CreateMap<MachineStatusDto, MachineStatus>();

        CreateMap<Piston, PistonStatusDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.ValveGroup, opt => opt.MapFrom(src => src.ValveGroup.ToString()))
            .ForMember(dest => dest.Motion, opt => opt.MapFrom(src => src.Motion.ToString()));

        CreateMap<DomainBendingParameters, BendingParametersDto>();
        CreateMap<BendingParametersDto, DomainBendingParameters>();

        CreateMap<BendingCalculationRequestDto, BendingCalculationRequestDto>();
        CreateMap<BendingCalculationResultDto, BendingCalculationResultDto>();

        // Request/Response mappings
        CreateMap<PistonMoveRequestDto, PistonControlDto>();
        CreateMap<PistonPositionRequestDto, PistonControlDto>();
        CreateMap<PistonJogRequestDto, PistonControlDto>();
        
        // Enum conversions
        CreateMap<PistonType, string>().ConvertUsing(src => src.ToString());
        CreateMap<string, PistonType>().ConvertUsing(src => Enum.Parse<PistonType>(src));
        
        CreateMap<MotionEnum, string>().ConvertUsing(src => src.ToString());
        CreateMap<string, MotionEnum>().ConvertUsing(src => Enum.Parse<MotionEnum>(src));
        
        CreateMap<ValveGroup, string>().ConvertUsing(src => src.ToString());
        CreateMap<string, ValveGroup>().ConvertUsing(src => Enum.Parse<ValveGroup>(src));
        
        CreateMap<RotationDirection, string>().ConvertUsing(src => src.ToString());
        CreateMap<string, RotationDirection>().ConvertUsing(src => Enum.Parse<RotationDirection>(src));
    }
} 