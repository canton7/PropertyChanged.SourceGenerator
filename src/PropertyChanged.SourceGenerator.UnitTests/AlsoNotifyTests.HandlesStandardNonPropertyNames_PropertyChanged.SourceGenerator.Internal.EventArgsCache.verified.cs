namespace PropertyChanged.SourceGenerator.Internal
{
    internal static class EventArgsCache
    {
        private static global::System.ComponentModel.PropertyChangedEventArgs _PropertyChanged_Empty;
        public static global::System.ComponentModel.PropertyChangedEventArgs PropertyChanged_Empty => _PropertyChanged_Empty ??= new global::System.ComponentModel.PropertyChangedEventArgs(@"");
        private static global::System.ComponentModel.PropertyChangedEventArgs _PropertyChanged_Foo;
        public static global::System.ComponentModel.PropertyChangedEventArgs PropertyChanged_Foo => _PropertyChanged_Foo ??= new global::System.ComponentModel.PropertyChangedEventArgs(@"Foo");
        private static global::System.ComponentModel.PropertyChangedEventArgs _PropertyChanged_Item__;
        public static global::System.ComponentModel.PropertyChangedEventArgs PropertyChanged_Item__ => _PropertyChanged_Item__ ??= new global::System.ComponentModel.PropertyChangedEventArgs(@"Item[]");
        private static global::System.ComponentModel.PropertyChangedEventArgs _PropertyChanged_Null;
        public static global::System.ComponentModel.PropertyChangedEventArgs PropertyChanged_Null => _PropertyChanged_Null ??= new global::System.ComponentModel.PropertyChangedEventArgs(null);
    }
}