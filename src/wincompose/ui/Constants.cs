//
//  WinCompose — a compose key for Windows — http://wincompose.info/
//
//  Copyright © 2013—2018 Sam Hocevar <sam@hocevar.net>
//
//  This program is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using System.Windows.Media;

namespace WinCompose
{
    public static class Constants
    {
        public static FontFamily PreferredFontFamily => new FontFamily(PreferredString);
        public static FontFamily KeyLabelFontFamily => new FontFamily(KeyLabelString);
        public static FontFamily FixedFontFamily => new FontFamily(FixedString);

        // We need Segoe UI Symbol, but we put it at the end because it messes
        // with combining character rendering (see #71). As for Symbola, it’s
        // not a Microsoft font, but it appears to be widespread enough.
        private static readonly string[] PreferredFonts =
        {
            "Segoe UI",
            "Lucida Sans Unicode",
            "Noto Sans",
            "Arial",
            "Arial Unicode MS",
            "Microsoft Sans Serif",
            "Times New Roman",
            "Global User Interface",
            "Portable User Interface",
            "Segoe UI Symbol",
            "Symbola",
            "Unifont",
            "Unifont Upper",
        };

        private static readonly string PreferredString = string.Join(", ", PreferredFonts);
        private static readonly string KeyLabelString = "Tahoma, " + PreferredString;
        private static readonly string FixedString = "Consolas, Courier New, " + PreferredString;
    }
}
