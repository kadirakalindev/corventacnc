namespace BendingMachine.Domain.Interfaces;

public interface IModbusClient
{
    // Connection Management
    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    bool IsConnected { get; }
    
    // Digital I/O Operations (Only Coils - for both read and write)
    Task<bool> ReadCoilAsync(int address);
    Task<bool[]> ReadCoilsAsync(int startAddress, int count);
    Task WriteCoilAsync(int address, bool value);
    Task WriteCoilsAsync(int startAddress, bool[] values);
    
    // Analog I/O Operations
    Task<ushort> ReadHoldingRegisterAsync(int address);
    Task<short> ReadHoldingRegisterAsSignedAsync(int address);
    Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count);
    Task WriteHoldingRegisterAsync(int address, ushort value);
    Task WriteHoldingRegistersAsync(int startAddress, ushort[] values);
    
    Task<ushort> ReadInputRegisterAsync(int address);
    Task<short> ReadInputRegisterAsSignedAsync(int address);
    Task<ushort[]> ReadInputRegistersAsync(int startAddress, int count);
    
    // Batch Operations for Performance
    Task<Dictionary<int, bool>> ReadCoilsBatchAsync(IEnumerable<int> addresses);
    Task<Dictionary<int, ushort>> ReadRegistersBatchAsync(IEnumerable<int> addresses);
    
    // Events
    event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
    event EventHandler<ModbusErrorEventArgs> ErrorOccurred;
}

public class ConnectionStatusChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string? Message { get; set; }
}

public class ModbusErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public int? Address { get; set; }
} 