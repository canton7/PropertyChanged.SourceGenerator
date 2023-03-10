using PropertyChanged.SourceGenerator;
using System;
using System.ComponentModel;

namespace Sandbox;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello World!");
        var vm = new SomeViewModel();
        vm.propertyfoo2 = 3;
    }
}

public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify] private int _foo2;

}
