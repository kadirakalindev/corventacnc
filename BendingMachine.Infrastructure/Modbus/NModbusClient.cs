using BendingMachine.Domain.Interfaces;
using NModbus;
using System.Net.Sockets;

namespace BendingMachine.Infrastructure.Modbus;

public class NModbusClient : IModbusClient, IDisposable
{
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly byte _slaveId;
    private readonly int _timeout;
    
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private bool _isConnected;
    private readonly object _lockObject = new object();
    
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<ModbusErrorEventArgs>? ErrorOccurred;
    
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    
    public NModbusClient(string ipAddress = "192.168.1.100", int port = 502, byte slaveId = 1, int timeoutMs = 3000)
    {
        _ipAddress = ipAddress;
        _port = port;
        _slaveId = slaveId;
        _timeout = timeoutMs;
    }
    
    public async Task<bool> ConnectAsync()
    {
        try
        {
            lock (_lockObject)
            {
                if (IsConnected)
                    return true;
                    
                // Dispose existing connections
                DisposeConnections();
                
                // Create new TCP client
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = _timeout;
                _tcpClient.SendTimeout = _timeout;
            }
            
            // Connect to Modbus device
            await _tcpClient.ConnectAsync(_ipAddress, _port);
            
            lock (_lockObject)
            {
                // Create Modbus master
                var factory = new ModbusFactory();
                _modbusMaster = factory.CreateMaster(_tcpClient);
                _modbusMaster.Transport.Retries = 3;
                _modbusMaster.Transport.WaitToRetryMilliseconds = 250;
                
                _isConnected = true;
            }
            
            OnConnectionStatusChanged(true, "Successfully connected to Modbus device");
            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Connection failed: {ex.Message}", ex);
            OnConnectionStatusChanged(false, $"Connection failed: {ex.Message}");
            
            lock (_lockObject)
            {
                DisposeConnections();
                _isConnected = false;
            }
            
            return false;
        }
    }
    
    public Task<bool> DisconnectAsync()
    {
        try
        {
            lock (_lockObject)
            {
                DisposeConnections();
                _isConnected = false;
            }
            
            OnConnectionStatusChanged(false, "Disconnected from Modbus device");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Disconnect failed: {ex.Message}", ex);
            return Task.FromResult(false);
        }
    }
    
    // Digital I/O Operations (Coils only)
    public async Task<bool> ReadCoilAsync(int address)
    {
        try
        {
            EnsureConnected();
            var result = await _modbusMaster!.ReadCoilsAsync(_slaveId, (ushort)address, 1);
            return result[0];
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read coil at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    public async Task<bool[]> ReadCoilsAsync(int startAddress, int count)
    {
        try
        {
            EnsureConnected();
            return await _modbusMaster!.ReadCoilsAsync(_slaveId, (ushort)startAddress, (ushort)count);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read coils at address {startAddress}, count {count}: {ex.Message}", ex, startAddress);
            throw;
        }
    }
    
    public async Task WriteCoilAsync(int address, bool value)
    {
        try
        {
            EnsureConnected();
            await _modbusMaster!.WriteSingleCoilAsync(_slaveId, (ushort)address, value);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to write coil at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    public async Task WriteCoilsAsync(int startAddress, bool[] values)
    {
        try
        {
            EnsureConnected();
            await _modbusMaster!.WriteMultipleCoilsAsync(_slaveId, (ushort)startAddress, values);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to write coils at address {startAddress}: {ex.Message}", ex, startAddress);
            throw;
        }
    }
    
    // Analog I/O Operations
    public async Task<ushort> ReadHoldingRegisterAsync(int address)
    {
        try
        {
            EnsureConnected();
            var result = await _modbusMaster!.ReadHoldingRegistersAsync(_slaveId, (ushort)address, 1);
            return result[0];
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read holding register at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    /// <summary>
    /// Holding register'ı signed short olarak okur (negatif değerler için)
    /// </summary>
    public async Task<short> ReadHoldingRegisterAsSignedAsync(int address)
    {
        try
        {
            EnsureConnected();
            var result = await _modbusMaster!.ReadHoldingRegistersAsync(_slaveId, (ushort)address, 1);
            return unchecked((short)result[0]); // Unsigned'dan signed'a dönüştür
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read holding register as signed at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    public async Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        try
        {
            EnsureConnected();
            return await _modbusMaster!.ReadHoldingRegistersAsync(_slaveId, (ushort)startAddress, (ushort)count);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read holding registers at address {startAddress}, count {count}: {ex.Message}", ex, startAddress);
            throw;
        }
    }
    
    public async Task WriteHoldingRegisterAsync(int address, ushort value)
    {
        try
        {
            EnsureConnected();
            await _modbusMaster!.WriteSingleRegisterAsync(_slaveId, (ushort)address, value);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to write holding register at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    public async Task WriteHoldingRegistersAsync(int startAddress, ushort[] values)
    {
        try
        {
            EnsureConnected();
            await _modbusMaster!.WriteMultipleRegistersAsync(_slaveId, (ushort)startAddress, values);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to write holding registers at address {startAddress}: {ex.Message}", ex, startAddress);
            throw;
        }
    }
    
    public async Task<ushort> ReadInputRegisterAsync(int address)
    {
        try
        {
            EnsureConnected();
            var result = await _modbusMaster!.ReadInputRegistersAsync(_slaveId, (ushort)address, 1);
            return result[0];
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read input register at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    /// <summary>
    /// Input register'ı signed short olarak okur (negatif değerler için - cetvel ve encoder okumaları)
    /// </summary>
    public async Task<short> ReadInputRegisterAsSignedAsync(int address)
    {
        try
        {
            EnsureConnected();
            var result = await _modbusMaster!.ReadInputRegistersAsync(_slaveId, (ushort)address, 1);
            return unchecked((short)result[0]); // Unsigned'dan signed'a dönüştür
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read input register as signed at address {address}: {ex.Message}", ex, address);
            throw;
        }
    }
    
    public async Task<ushort[]> ReadInputRegistersAsync(int startAddress, int count)
    {
        try
        {
            EnsureConnected();
            return await _modbusMaster!.ReadInputRegistersAsync(_slaveId, (ushort)startAddress, (ushort)count);
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Failed to read input registers at address {startAddress}, count {count}: {ex.Message}", ex, startAddress);
            throw;
        }
    }
    
    // Batch Operations
    public async Task<Dictionary<int, bool>> ReadCoilsBatchAsync(IEnumerable<int> addresses)
    {
        var result = new Dictionary<int, bool>();
        
        foreach (var address in addresses)
        {
            try
            {
                result[address] = await ReadCoilAsync(address);
            }
            catch
            {
                result[address] = false; // Default value on error
            }
        }
        
        return result;
    }
    
    public async Task<Dictionary<int, ushort>> ReadRegistersBatchAsync(IEnumerable<int> addresses)
    {
        var result = new Dictionary<int, ushort>();
        
        foreach (var address in addresses)
        {
            try
            {
                result[address] = await ReadInputRegisterAsync(address);
            }
            catch
            {
                result[address] = 0; // Default value on error
            }
        }
        
        return result;
    }
    
    // Helper Methods
    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to Modbus device. Call ConnectAsync() first.");
        }
    }
    
    private void DisposeConnections()
    {
        try
        {
            _modbusMaster?.Dispose();
            _tcpClient?.Close();
            _tcpClient?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
        finally
        {
            _modbusMaster = null;
            _tcpClient = null;
        }
    }
    
    private void OnConnectionStatusChanged(bool isConnected, string? message = null)
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
        {
            IsConnected = isConnected,
            Message = message
        });
    }
    
    private void OnErrorOccurred(string message, Exception? exception = null, int? address = null)
    {
        ErrorOccurred?.Invoke(this, new ModbusErrorEventArgs
        {
            ErrorMessage = message,
            Exception = exception,
            Address = address
        });
    }
    
    public void Dispose()
    {
        lock (_lockObject)
        {
            DisposeConnections();
            _isConnected = false;
        }
    }
} 