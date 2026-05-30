namespace _Project.CodeBase
{
    public enum AppGameState
    {
        Initializing,  // Bootstrap ещё не завершил работу
        MainMenu,      // Игрок в меню, не подключён
        Connecting,    // Идёт подключение к Photon
        InLobby,       // Подключились, ждём ready всех игроков
        InMatch,       // Матч идёт
        MatchEnded,    // Матч завершён, показываем результаты
        Disconnecting  // Отключение в процессе
    }
}

