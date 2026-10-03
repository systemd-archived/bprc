using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

[Serializable]
public abstract class SettingsBase : INotifyPropertyChanged
{
	protected event PropertyChangedEventHandler PropertyChanged;

	event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
	{
		add
		{
			PropertyChanged += value;
		}
		remove
		{
			PropertyChanged -= value;
		}
	}

	public virtual void Apply()
	{
	}

	protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
