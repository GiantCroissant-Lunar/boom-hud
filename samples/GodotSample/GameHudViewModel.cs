using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GodotSample;

public class GameHudViewModel : IGameHudViewModel
{
    private int _playerHealth = 100;
    private int _playerMaxHealth = 100;
    private int _playerMana = 50;
    private int _playerMaxMana = 100;
    private int _score = 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? PlayerHealth => _playerHealth;
    
    public object? PlayerHealthPercent => (double)_playerHealth / _playerMaxHealth * 100.0; // ProgressBar expects 0-100 usually or 0-1? 
    // Wait, Godot ProgressBar Range is usually 0-100 by default unless changed. 
    // Default Min=0, Max=100.
    // The generated code for ProgressBar didn't set Max/Min explicitly except standard defaults? 
    // Actually GodotGenerator doesn't set Range min/max from schema yet unless properties provided.
    // Let's assume 0-100 for now.
    
    public object? PlayerManaPercent => (double)_playerMana / _playerMaxMana * 100.0;

    public object? Score => _score;

    public object? TogglePauseCommand { get; }

    public GameHudViewModel()
    {
        TogglePauseCommand = new SimpleCommand(TogglePause);
        
        // Simulate some game loop updates
        _ = SimulateGameLoop();
    }

    private void TogglePause()
    {
        Godot.GD.Print("Pause Toggled!");
        _score += 100;
        OnPropertyChanged(nameof(Score));
    }

    private async Task SimulateGameLoop()
    {
        var random = new Random();
        while (true)
        {
            await Task.Delay(1000);
            
            _playerHealth -= random.Next(0, 10);
            if (_playerHealth < 0) _playerHealth = 100;
            
            OnPropertyChanged(nameof(PlayerHealth));
            OnPropertyChanged(nameof(PlayerHealthPercent));

            _playerMana += random.Next(0, 10);
            if (_playerMana > _playerMaxMana) _playerMana = 0;
            
            OnPropertyChanged(nameof(PlayerManaPercent));
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class SimpleCommand : ICommand
{
    private readonly Action _action;

    public SimpleCommand(Action action)
    {
        _action = action;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        _action();
    }
}
