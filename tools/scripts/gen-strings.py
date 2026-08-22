#!/usr/bin/env python3
"""Генерирует Strings.cs из Strings.resx.

Штатный ResXFileCodeGenerator работает только внутри Visual Studio, а сборка
идёт через `dotnet build`, поэтому строго типизированный доступ к ресурсам
генерируем сами. Правьте Strings.resx и запускайте:

    python3 tools/scripts/gen-strings.py
"""
from __future__ import annotations

import pathlib
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
RESX = ROOT / "src/DupFinder.App/Resources/Strings.resx"
OUT = ROOT / "src/DupFinder.App/Resources/Strings.cs"

HEADER = '''using System.Globalization;
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
'''


def escape_doc(text: str) -> str:
    return (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace("\n", " ")
        .strip()
    )


def main() -> None:
    tree = ET.parse(RESX)
    parts = [HEADER]
    for data in tree.getroot().findall("data"):
        key = data.get("name")
        if key is None:
            continue
        value = data.findtext("value") or ""
        parts.append(
            f"\n    /// <summary>{escape_doc(value)}</summary>\n"
            f"    public static string {key} => Get(nameof({key}));\n"
        )
    parts.append("}\n")
    OUT.write_text("".join(parts), encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)}: {len(parts) - 2} строк(и)")


if __name__ == "__main__":
    main()
