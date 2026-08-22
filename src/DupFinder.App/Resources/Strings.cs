using System.Globalization;
using System.Resources;

namespace DupFinder.App.Resources;

/// <summary>
/// Доступ к строкам интерфейса. Файл сгенерирован из Strings.resx скриптом
/// tools/scripts/gen-strings.py — правьте .resx, а не этот файл.
/// Локализация заведена с первого дня, как требует ТЗ §11.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("DupFinder.App.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>Язык интерфейса. Смена подхватывается при следующем чтении строки.</summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>Строка по ключу; если её нет — сам ключ, чтобы окно не падало.</summary>
    public static string Get(string key) => Manager.GetString(key, Culture) ?? key;

    /// <summary>DupFinder Pro</summary>
    public static string AppTitle => Get(nameof(AppTitle));

    /// <summary>Точные копии • та же съёмка • визуально похожие</summary>
    public static string AppSubtitle => Get(nameof(AppSubtitle));

    /// <summary>Сканирование</summary>
    public static string TabScan => Get(nameof(TabScan));

    /// <summary>Результаты</summary>
    public static string TabResults => Get(nameof(TabResults));

    /// <summary>ПАПКИ ДЛЯ ПРОВЕРКИ</summary>
    public static string SectionFolders => Get(nameof(SectionFolders));

    /// <summary>РЕЖИМ ПОИСКА</summary>
    public static string SectionMode => Get(nameof(SectionMode));

    /// <summary>ФИЛЬТРЫ</summary>
    public static string SectionFilters => Get(nameof(SectionFilters));

    /// <summary>ОРИГИНАЛОМ СЧИТАТЬ</summary>
    public static string SectionOriginal => Get(nameof(SectionOriginal));

    /// <summary>ПАПКА-ЭТАЛОН</summary>
    public static string SectionReference => Get(nameof(SectionReference));

    /// <summary>СКОРОСТЬ</summary>
    public static string SectionPerformance => Get(nameof(SectionPerformance));

    /// <summary>Добавить папку</summary>
    public static string AddFolder => Get(nameof(AddFolder));

    /// <summary>Убрать</summary>
    public static string RemoveFolder => Get(nameof(RemoveFolder));

    /// <summary>Перетащите папки прямо в окно или нажмите «Добавить папку».</summary>
    public static string FoldersHint => Get(nameof(FoldersHint));

    /// <summary>Включая подпапки</summary>
    public static string IncludeSubfolders => Get(nameof(IncludeSubfolders));

    /// <summary>Скрытые файлы</summary>
    public static string IncludeHidden => Get(nameof(IncludeHidden));

    /// <summary>Системные файлы</summary>
    public static string IncludeSystem => Get(nameof(IncludeSystem));

    /// <summary>Точные копии</summary>
    public static string ModeExact => Get(nameof(ModeExact));

    /// <summary>Файлы совпадают байт в байт. Самый надёжный режим — такие копии можно удалять спокойно.</summary>
    public static string ModeExactHint => Get(nameof(ModeExactHint));

    /// <summary>Та же съёмка (EXIF)</summary>
    public static string ModeSameShot => Get(nameof(ModeSameShot));

    /// <summary>Одинаковые дата съёмки, камера и размер кадра. Ловит одно фото в HEIC и JPG. Появится на следующем этапе.</summary>
    public static string ModeSameShotHint => Get(nameof(ModeSameShotHint));

    /// <summary>Визуально похожие</summary>
    public static string ModeSimilar => Get(nameof(ModeSimilar));

    /// <summary>Сравнение картинки «на глаз»: пересжатые и уменьшенные копии. Появится на следующем этапе.</summary>
    public static string ModeSimilarHint => Get(nameof(ModeSimilarHint));

    /// <summary>Тип файлов</summary>
    public static string FilterKind => Get(nameof(FilterKind));

    /// <summary>Все файлы</summary>
    public static string KindAll => Get(nameof(KindAll));

    /// <summary>Фото</summary>
    public static string KindPhoto => Get(nameof(KindPhoto));

    /// <summary>Видео</summary>
    public static string KindVideo => Get(nameof(KindVideo));

    /// <summary>Аудио</summary>
    public static string KindAudio => Get(nameof(KindAudio));

    /// <summary>Документы</summary>
    public static string KindDocument => Get(nameof(KindDocument));

    /// <summary>Архивы</summary>
    public static string KindArchive => Get(nameof(KindArchive));

    /// <summary>Минимальный размер, КБ</summary>
    public static string MinSizeKb => Get(nameof(MinSizeKb));

    /// <summary>Максимальный размер, МБ (0 — без ограничения)</summary>
    public static string MaxSizeMb => Get(nameof(MaxSizeMb));

    /// <summary>Не проверять файлы по маске (через точку с запятой)</summary>
    public static string ExcludeMasks => Get(nameof(ExcludeMasks));

    /// <summary>Самый старый файл</summary>
    public static string OriginalOldest => Get(nameof(OriginalOldest));

    /// <summary>Самый новый файл</summary>
    public static string OriginalNewest => Get(nameof(OriginalNewest));

    /// <summary>С самым коротким путём</summary>
    public static string OriginalShortest => Get(nameof(OriginalShortest));

    /// <summary>Наибольшее разрешение</summary>
    public static string OriginalResolution => Get(nameof(OriginalResolution));

    /// <summary>Самый большой файл</summary>
    public static string OriginalLargest => Get(nameof(OriginalLargest));

    /// <summary>Исходный формат (HEIC/RAW/PNG &gt; JPG)</summary>
    public static string OriginalFormat => Get(nameof(OriginalFormat));

    /// <summary>Из этой папки ничего никогда не удаляется.</summary>
    public static string ReferenceHint => Get(nameof(ReferenceHint));

    /// <summary>Сверять побайтно вместо SHA-256</summary>
    public static string ConfirmBytewise => Get(nameof(ConfirmBytewise));

    /// <summary>Тип диска</summary>
    public static string DiskType => Get(nameof(DiskType));

    /// <summary>Определить самому</summary>
    public static string DiskAuto => Get(nameof(DiskAuto));

    /// <summary>SSD — быстрый</summary>
    public static string DiskSsd => Get(nameof(DiskSsd));

    /// <summary>Обычный жёсткий диск</summary>
    public static string DiskHdd => Get(nameof(DiskHdd));

    /// <summary>Сетевая папка</summary>
    public static string DiskNetwork => Get(nameof(DiskNetwork));

    /// <summary>Начать поиск</summary>
    public static string Start => Get(nameof(Start));

    /// <summary>Отменить</summary>
    public static string Cancel => Get(nameof(Cancel));

    /// <summary>Готов к работе.</summary>
    public static string Ready => Get(nameof(Ready));

    /// <summary>Собираю список файлов</summary>
    public static string StageEnumerating => Get(nameof(StageEnumerating));

    /// <summary>Отсеиваю по размеру</summary>
    public static string StageGrouping => Get(nameof(StageGrouping));

    /// <summary>Быстрая проверка начала файлов</summary>
    public static string StagePartial => Get(nameof(StagePartial));

    /// <summary>Проверка середины и конца</summary>
    public static string StageMidTail => Get(nameof(StageMidTail));

    /// <summary>Полная сверка содержимого</summary>
    public static string StageFull => Get(nameof(StageFull));

    /// <summary>Подтверждаю совпадения</summary>
    public static string StageConfirming => Get(nameof(StageConfirming));

    /// <summary>Готово</summary>
    public static string StageDone => Get(nameof(StageDone));

    /// <summary>Поиск отменён.</summary>
    public static string StageCancelled => Get(nameof(StageCancelled));

    /// <summary>Результатов пока нет</summary>
    public static string EmptyResultsTitle => Get(nameof(EmptyResultsTitle));

    /// <summary>Добавьте папку на вкладке «Сканирование» и нажмите «Начать поиск».</summary>
    public static string EmptyResultsHint => Get(nameof(EmptyResultsHint));

    /// <summary>Ничего не найдено — копий нет.</summary>
    public static string NothingFound => Get(nameof(NothingFound));

    /// <summary>#</summary>
    public static string ColumnGroup => Get(nameof(ColumnGroup));

    /// <summary>РОЛЬ</summary>
    public static string ColumnRole => Get(nameof(ColumnRole));

    /// <summary>КАК НАЙДЕНО</summary>
    public static string ColumnMatch => Get(nameof(ColumnMatch));

    /// <summary>ФАЙЛ</summary>
    public static string ColumnFile => Get(nameof(ColumnFile));

    /// <summary>ТИП</summary>
    public static string ColumnKind => Get(nameof(ColumnKind));

    /// <summary>ПИКС.</summary>
    public static string ColumnPixels => Get(nameof(ColumnPixels));

    /// <summary>РАЗМЕР</summary>
    public static string ColumnSize => Get(nameof(ColumnSize));

    /// <summary>ИЗМЕНЁН</summary>
    public static string ColumnModified => Get(nameof(ColumnModified));

    /// <summary>ПАПКА</summary>
    public static string ColumnFolder => Get(nameof(ColumnFolder));

    /// <summary>★ Оригинал</summary>
    public static string RoleOriginal => Get(nameof(RoleOriginal));

    /// <summary>Копия</summary>
    public static string RoleCopy => Get(nameof(RoleCopy));

    /// <summary>🔒 Эталон</summary>
    public static string RoleProtected => Get(nameof(RoleProtected));

    /// <summary>Точная копия</summary>
    public static string MatchExact => Get(nameof(MatchExact));

    /// <summary>Та же съёмка</summary>
    public static string MatchSameShot => Get(nameof(MatchSameShot));

    /// <summary>Похоже</summary>
    public static string MatchSimilar => Get(nameof(MatchSimilar));

    /// <summary>— эталон группы</summary>
    public static string MatchGroupOriginal => Get(nameof(MatchGroupOriginal));

    /// <summary>Отметить все копии</summary>
    public static string MarkCopies => Get(nameof(MarkCopies));

    /// <summary>Инвертировать</summary>
    public static string MarkInvert => Get(nameof(MarkInvert));

    /// <summary>Снять все</summary>
    public static string MarkClear => Get(nameof(MarkClear));

    /// <summary>Показать в Проводнике</summary>
    public static string RevealInExplorer => Get(nameof(RevealInExplorer));

    /// <summary>Копировать путь</summary>
    public static string CopyPath => Get(nameof(CopyPath));

    /// <summary>Сделать оригиналом</summary>
    public static string MakeOriginal => Get(nameof(MakeOriginal));

    /// <summary>Удалить отмеченные (в Корзину)</summary>
    public static string DeleteMarked => Get(nameof(DeleteMarked));

    /// <summary>Журнал</summary>
    public static string OpenLog => Get(nameof(OpenLog));

    /// <summary>Фильтр по имени или пути</summary>
    public static string FilterPlaceholder => Get(nameof(FilterPlaceholder));

    /// <summary>Все роли</summary>
    public static string FilterRoleAll => Get(nameof(FilterRoleAll));

    /// <summary>Только копии</summary>
    public static string FilterRoleCopies => Get(nameof(FilterRoleCopies));

    /// <summary>Только оригиналы</summary>
    public static string FilterRoleOriginals => Get(nameof(FilterRoleOriginals));

    /// <summary>Статистика появится после сканирования.</summary>
    public static string SummaryPlaceholder => Get(nameof(SummaryPlaceholder));

    /// <summary>Проверено: {0}   •   Без копий: {1}   •   В группах: {2} (групп: {3})   •   Лишних копий: {4}   •   Освободить: {5}   •   Отмечено: {6} ({7})</summary>
    public static string SummaryFormat => Get(nameof(SummaryFormat));

    /// <summary>{0}: {1} из {2}</summary>
    public static string ProgressFormat => Get(nameof(ProgressFormat));

    /// <summary>{0}/с</summary>
    public static string SpeedFormat => Get(nameof(SpeedFormat));

    /// <summary>осталось ~{0}</summary>
    public static string RemainingFormat => Get(nameof(RemainingFormat));

    /// <summary>прошло {0}</summary>
    public static string ElapsedFormat => Get(nameof(ElapsedFormat));

    /// <summary>✓ Готово за {0}. Найдено групп: {1}.</summary>
    public static string DoneFormat => Get(nameof(DoneFormat));

    /// <summary>Сначала добавьте хотя бы одну папку для проверки.</summary>
    public static string NoFoldersSelected => Get(nameof(NoFoldersSelected));

    /// <summary>Папка не найдена: {0}</summary>
    public static string FolderNotFound => Get(nameof(FolderNotFound));

    /// <summary>Папка-эталон не найдена: {0}</summary>
    public static string ReferenceNotFound => Get(nameof(ReferenceNotFound));

    /// <summary>Отметьте галочками файлы, которые нужно удалить.</summary>
    public static string NothingMarked => Get(nameof(NothingMarked));

    /// <summary>Подтверждение</summary>
    public static string ConfirmDeleteTitle => Get(nameof(ConfirmDeleteTitle));

    /// <summary>Отправить в Корзину {0} файл(ов), {1}?</summary>
    public static string ConfirmDeleteFormat => Get(nameof(ConfirmDeleteFormat));

    /// <summary>⚠ В {0} группах отмечены ВСЕ файлы, включая оригинал.</summary>
    public static string WarnWholeGroupFormat => Get(nameof(WarnWholeGroupFormat));

    /// <summary>⚠ {0} файл(ов) найдены не побайтно. Посмотрите их глазами перед удалением.</summary>
    public static string WarnFuzzyFormat => Get(nameof(WarnFuzzyFormat));

    /// <summary>{0} файл(ов) из папки-эталона будут пропущены.</summary>
    public static string WarnProtectedFormat => Get(nameof(WarnProtectedFormat));

    /// <summary>В Корзину отправлено: {0}.</summary>
    public static string DeleteResultFormat => Get(nameof(DeleteResultFormat));

    /// <summary>Не удалось удалить: {0}.</summary>
    public static string DeleteFailedFormat => Get(nameof(DeleteFailedFormat));

    /// <summary>Открыть папку с журналом?</summary>
    public static string ErrorOpenLogPrompt => Get(nameof(ErrorOpenLogPrompt));

    /// <summary>Во время поиска произошла ошибка: {0}</summary>
    public static string ErrorScanFormat => Get(nameof(ErrorScanFormat));

    /// <summary>Этот режим появится на следующем этапе. Пока доступны точные копии.</summary>
    public static string ModeNotAvailable => Get(nameof(ModeNotAvailable));
}
