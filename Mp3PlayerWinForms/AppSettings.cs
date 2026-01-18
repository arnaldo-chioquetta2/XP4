namespace XP3
{
    public static class AppSettings
    {
        // O ÚNICO lugar onde você vai trocar de true para false
        // public static bool IsDevelopment = true;
        public static bool IsDevelopment = System.Diagnostics.Debugger.IsAttached;

        // Propriedade auxiliar para o volume inicial
        public static float InitialVolume => IsDevelopment ? 0.01f : 1.0f;
    }
}