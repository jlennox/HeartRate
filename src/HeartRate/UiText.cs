using System;
using System.Collections.Generic;
using System.Globalization;

namespace HeartRate;

// Compiled into HeartRate.exe. English text is also the fallback for unsupported cultures.
internal static class UiText
{
    // A shared override also applies to readings delivered by existing worker threads.
    private static volatile CultureInfo _selectedCulture;

    internal static string NormalizeLanguage(string language) => language switch
    {
        "en" or "ja" or "zh-Hans" or "de" or "hi" or "ta" or "te" or "es" or "fr" or "pt-BR" => language,
        _ => null
    };

    internal static void SelectLanguage(string language)
    {
        language = NormalizeLanguage(language);
        _selectedCulture = language == null ? null : CultureInfo.GetCultureInfo(language);
    }

    // Columns: Japanese, Simplified Chinese, German, Hindi, Tamil, Telugu,
    // Spanish, French, Brazilian Portuguese.
    private static readonly Dictionary<string, string[]> Translations = new(StringComparer.Ordinal)
    {
        ["Heart rate monitor"] = new[]
        {
            "心拍数モニター",
            "心率监测器",
            "Herzfrequenzmonitor",
            "हृदय गति मॉनिटर",
            "இதயத் துடிப்பு கண்காணிப்பான்",
            "హృదయ స్పందన మానిటర్",
            "Monitor de frecuencia cardíaca",
            "Moniteur de fréquence cardiaque",
            "Monitor de frequência cardíaca",
        },
        ["Starting..."] = new[]
        {
            "起動中...",
            "正在启动...",
            "Wird gestartet...",
            "शुरू हो रहा है...",
            "தொடங்குகிறது...",
            "ప్రారంభమవుతోంది...",
            "Iniciando...",
            "Démarrage...",
            "Iniciando...",
        },
        ["Select icon font..."] = new[]
        {
            "アイコンのフォントを選択...",
            "选择图标字体...",
            "Symbolschrift auswählen...",
            "आइकन का फ़ॉन्ट चुनें...",
            "சின்னத்தின் எழுத்துருவைத் தேர்ந்தெடு...",
            "చిహ్నం ఫాంట్‌ను ఎంచుకోండి...",
            "Seleccionar fuente del icono...",
            "Choisir la police de l’icône...",
            "Selecionar fonte do ícone...",
        },
        ["Edit icon font color..."] = new[]
        {
            "アイコンの文字色を変更...",
            "更改图标字体颜色...",
            "Symbolschriftfarbe ändern...",
            "आइकन के फ़ॉन्ट का रंग बदलें...",
            "சின்னத்தின் எழுத்து நிறத்தை மாற்று...",
            "చిహ్నం ఫాంట్ రంగును మార్చండి...",
            "Cambiar color de fuente del icono...",
            "Modifier la couleur du texte de l’icône...",
            "Alterar cor da fonte do ícone...",
        },
        ["Edit icon font warning color..."] = new[]
        {
            "アイコンの警告文字色を変更...",
            "更改图标警告字体颜色...",
            "Warnfarbe der Symbolschrift ändern...",
            "आइकन के फ़ॉन्ट का चेतावनी रंग बदलें...",
            "சின்னத்தின் எச்சரிக்கை எழுத்து நிறத்தை மாற்று...",
            "చిహ్నం ఫాంట్ హెచ్చరిక రంగును మార్చండి...",
            "Cambiar color de advertencia del icono...",
            "Modifier la couleur d’avertissement du texte de l’icône...",
            "Alterar cor de alerta da fonte do ícone...",
        },
        ["Select window font..."] = new[]
        {
            "ウィンドウのフォントを選択...",
            "选择窗口字体...",
            "Fensterschrift auswählen...",
            "विंडो का फ़ॉन्ट चुनें...",
            "சாளரத்தின் எழுத்துருவைத் தேர்ந்தெடு...",
            "విండో ఫాంట్‌ను ఎంచుకోండి...",
            "Seleccionar fuente de la ventana...",
            "Choisir la police de la fenêtre...",
            "Selecionar fonte da janela...",
        },
        ["Do not scale font"] = new[]
        {
            "フォントを自動拡大・縮小しない",
            "不自动缩放字体",
            "Schriftgröße beibehalten",
            "फ़ॉन्ट का आकार अपने आप न बदलें",
            "எழுத்துரு அளவைத் தானாக மாற்ற வேண்டாம்",
            "ఫాంట్ పరిమాణాన్ని స్వయంచాలకంగా మార్చవద్దు",
            "No ajustar el tamaño de la fuente",
            "Conserver la taille de la police",
            "Não redimensionar a fonte",
        },
        ["Edit window font color..."] = new[]
        {
            "ウィンドウの文字色を変更...",
            "更改窗口字体颜色...",
            "Fensterschriftfarbe ändern...",
            "विंडो के फ़ॉन्ट का रंग बदलें...",
            "சாளரத்தின் எழுத்து நிறத்தை மாற்று...",
            "విండో ఫాంట్ రంగును మార్చండి...",
            "Cambiar color de fuente de la ventana...",
            "Modifier la couleur du texte de la fenêtre...",
            "Alterar cor da fonte da janela...",
        },
        ["Edit window font warning color..."] = new[]
        {
            "ウィンドウの警告文字色を変更...",
            "更改窗口警告字体颜色...",
            "Warnfarbe der Fensterschrift ändern...",
            "विंडो के फ़ॉन्ट का चेतावनी रंग बदलें...",
            "சாளரத்தின் எச்சரிக்கை எழுத்து நிறத்தை மாற்று...",
            "విండో ఫాంట్ హెచ్చరిక రంగును మార్చండి...",
            "Cambiar color de advertencia de la ventana...",
            "Modifier la couleur d’avertissement du texte de la fenêtre...",
            "Alterar cor de alerta da fonte da janela...",
        },
        ["Text alignment"] = new[]
        {
            "文字の配置",
            "文本对齐",
            "Textausrichtung",
            "टेक्स्ट का संरेखण",
            "உரை சீரமைப்பு",
            "వచన అమరిక",
            "Alineación del texto",
            "Alignement du texte",
            "Alinhamento do texto",
        },
        ["Select background image..."] = new[]
        {
            "背景画像を選択...",
            "选择背景图片...",
            "Hintergrundbild auswählen...",
            "पृष्ठभूमि की छवि चुनें...",
            "பின்னணிப் படத்தைத் தேர்ந்தெடு...",
            "నేపథ్య చిత్రాన్ని ఎంచుకోండి...",
            "Seleccionar imagen de fondo...",
            "Choisir l’image d’arrière-plan...",
            "Selecionar imagem de fundo...",
        },
        ["Remove background image"] = new[]
        {
            "背景画像を削除",
            "移除背景图片",
            "Hintergrundbild entfernen",
            "पृष्ठभूमि की छवि हटाएँ",
            "பின்னணிப் படத்தை அகற்று",
            "నేపథ్య చిత్రాన్ని తొలగించండి",
            "Quitar imagen de fondo",
            "Supprimer l’image d’arrière-plan",
            "Remover imagem de fundo",
        },
        ["Background image position"] = new[]
        {
            "背景画像の配置",
            "背景图片位置",
            "Hintergrundbildposition",
            "पृष्ठभूमि की छवि की स्थिति",
            "பின்னணிப் படத்தின் நிலை",
            "నేపథ్య చిత్రం స్థానం",
            "Posición de la imagen de fondo",
            "Position de l’image d’arrière-plan",
            "Posição da imagem de fundo",
        },
        ["Background image layout"] = new[]
        {
            "背景画像のレイアウト",
            "背景图片布局",
            "Hintergrundbildanordnung",
            "पृष्ठभूमि की छवि का लेआउट",
            "பின்னணிப் பட அமைப்பு",
            "నేపథ్య చిత్రం అమరిక",
            "Disposición de la imagen de fondo",
            "Disposition de l’image d’arrière-plan",
            "Layout da imagem de fundo",
        },
        ["Edit settings XML..."] = new[]
        {
            "設定XMLを編集...",
            "编辑设置 XML...",
            "Einstellungs-XML bearbeiten...",
            "सेटिंग्स XML संपादित करें...",
            "அமைப்புகளின் XML-ஐத் திருத்து...",
            "సెట్టింగ్‌ల XMLను సవరించండి...",
            "Editar configuración XML...",
            "Modifier les paramètres XML...",
            "Editar configurações XML...",
        },
        ["Exit"] = new[]
        {
            "終了",
            "退出",
            "Beenden",
            "बंद करें",
            "வெளியேறு",
            "నిష్క్రమించు",
            "Salir",
            "Quitter",
            "Sair",
        },
        ["Set heart rate file..."] = new[]
        {
            "心拍数ファイルを設定...",
            "设置心率文件...",
            "Herzfrequenzdatei festlegen...",
            "हृदय गति की फ़ाइल सेट करें...",
            "இதயத் துடிப்புக் கோப்பை அமை...",
            "హృదయ స్పందన ఫైల్‌ను సెట్ చేయండి...",
            "Establecer archivo de frecuencia cardíaca...",
            "Définir le fichier de fréquence cardiaque...",
            "Definir arquivo de frequência cardíaca...",
        },
        ["Unset heart rate file"] = new[]
        {
            "心拍数ファイルの設定を解除",
            "取消心率文件设置",
            "Herzfrequenzdatei deaktivieren",
            "हृदय गति की फ़ाइल की सेटिंग हटाएँ",
            "இதயத் துடிப்புக் கோப்பு அமைப்பை நீக்கு",
            "హృదయ స్పందన ఫైల్ సెట్టింగ్‌ను తొలగించండి",
            "Desactivar archivo de frecuencia cardíaca",
            "Désactiver le fichier de fréquence cardiaque",
            "Desativar arquivo de frequência cardíaca",
        },
        ["Set CSV output file..."] = new[]
        {
            "CSV出力ファイルを設定...",
            "设置 CSV 输出文件...",
            "CSV-Ausgabedatei festlegen...",
            "CSV आउटपुट फ़ाइल सेट करें...",
            "CSV வெளியீட்டுக் கோப்பை அமை...",
            "CSV అవుట్‌పుట్ ఫైల్‌ను సెట్ చేయండి...",
            "Establecer archivo de salida CSV...",
            "Définir le fichier de sortie CSV...",
            "Definir arquivo de saída CSV...",
        },
        ["Unset CSV output file"] = new[]
        {
            "CSV出力ファイルの設定を解除",
            "取消 CSV 输出文件设置",
            "CSV-Ausgabedatei deaktivieren",
            "CSV आउटपुट फ़ाइल की सेटिंग हटाएँ",
            "CSV வெளியீட்டுக் கோப்பு அமைப்பை நீக்கு",
            "CSV అవుట్‌పుట్ ఫైల్ సెట్టింగ్‌ను తొలగించండి",
            "Desactivar archivo de salida CSV",
            "Désactiver le fichier de sortie CSV",
            "Desativar arquivo de saída CSV",
        },
        ["Set IBI file..."] = new[]
        {
            "IBIファイルを設定...",
            "设置 IBI 文件...",
            "IBI-Datei festlegen...",
            "IBI फ़ाइल सेट करें...",
            "IBI கோப்பை அமை...",
            "IBI ఫైల్‌ను సెట్ చేయండి...",
            "Establecer archivo IBI...",
            "Définir le fichier IBI...",
            "Definir arquivo IBI...",
        },
        ["Unset IBI file"] = new[]
        {
            "IBIファイルの設定を解除",
            "取消 IBI 文件设置",
            "IBI-Datei deaktivieren",
            "IBI फ़ाइल की सेटिंग हटाएँ",
            "IBI கோப்பு அமைப்பை நீக்கு",
            "IBI ఫైల్ సెట్టింగ్‌ను తొలగించండి",
            "Desactivar archivo IBI",
            "Désactiver le fichier IBI",
            "Desativar arquivo IBI",
        },
        ["TopLeft"] = new[]
        {
            "左上",
            "左上",
            "Oben links",
            "ऊपर बाएँ",
            "மேல் இடது",
            "ఎగువ ఎడమ",
            "Arriba a la izquierda",
            "En haut à gauche",
            "Superior esquerdo",
        },
        ["TopCenter"] = new[]
        {
            "上中央",
            "顶部居中",
            "Oben mittig",
            "ऊपर बीच में",
            "மேல் நடு",
            "ఎగువ మధ్య",
            "Arriba en el centro",
            "En haut au centre",
            "Superior central",
        },
        ["TopRight"] = new[]
        {
            "右上",
            "右上",
            "Oben rechts",
            "ऊपर दाएँ",
            "மேல் வலது",
            "ఎగువ కుడి",
            "Arriba a la derecha",
            "En haut à droite",
            "Superior direito",
        },
        ["MiddleLeft"] = new[]
        {
            "左中央",
            "左侧居中",
            "Mitte links",
            "बीच में बाएँ",
            "நடு இடது",
            "మధ్య ఎడమ",
            "En el medio a la izquierda",
            "Au milieu à gauche",
            "Meio esquerdo",
        },
        ["MiddleCenter"] = new[]
        {
            "中央",
            "居中",
            "Zentriert",
            "बिल्कुल बीच में",
            "மையம்",
            "మధ్యలో",
            "En el centro",
            "Au centre",
            "Centro",
        },
        ["MiddleRight"] = new[]
        {
            "右中央",
            "右侧居中",
            "Mitte rechts",
            "बीच में दाएँ",
            "நடு வலது",
            "మధ్య కుడి",
            "En el medio a la derecha",
            "Au milieu à droite",
            "Meio direito",
        },
        ["BottomLeft"] = new[]
        {
            "左下",
            "左下",
            "Unten links",
            "नीचे बाएँ",
            "கீழ் இடது",
            "దిగువ ఎడమ",
            "Abajo a la izquierda",
            "En bas à gauche",
            "Inferior esquerdo",
        },
        ["BottomCenter"] = new[]
        {
            "下中央",
            "底部居中",
            "Unten mittig",
            "नीचे बीच में",
            "கீழ் நடு",
            "దిగువ మధ్య",
            "Abajo en el centro",
            "En bas au centre",
            "Inferior central",
        },
        ["BottomRight"] = new[]
        {
            "右下",
            "右下",
            "Unten rechts",
            "नीचे दाएँ",
            "கீழ் வலது",
            "దిగువ కుడి",
            "Abajo a la derecha",
            "En bas à droite",
            "Inferior direito",
        },
        ["None"] = new[]
        {
            "なし",
            "无",
            "Keine",
            "कोई नहीं",
            "ஏதுமில்லை",
            "ఏదీ లేదు",
            "Ninguna",
            "Aucune",
            "Nenhum",
        },
        ["Tile"] = new[]
        {
            "並べて表示",
            "平铺",
            "Kacheln",
            "टाइल की तरह दोहराएँ",
            "அடுக்குகளாக நிரப்பு",
            "పలకలుగా అమర్చు",
            "Mosaico",
            "Mosaïque",
            "Lado a lado",
        },
        ["Center"] = new[]
        {
            "中央に表示",
            "居中",
            "Zentrieren",
            "बीच में रखें",
            "மையத்தில் வை",
            "మధ్యలో ఉంచు",
            "Centrar",
            "Centrer",
            "Centralizar",
        },
        ["Stretch"] = new[]
        {
            "引き伸ばして表示",
            "拉伸",
            "Strecken",
            "खींचकर भरें",
            "நீட்டி நிரப்பு",
            "సాగదీయి",
            "Estirar",
            "Étirer",
            "Esticar",
        },
        ["Zoom"] = new[]
        {
            "縦横比を維持して拡大・縮小",
            "等比缩放",
            "Proportional skalieren",
            "अनुपात बनाए रखकर आकार बदलें",
            "விகிதத்தைப் பேணி அளவை மாற்று",
            "నిష్పత్తిని కాపాడుతూ పరిమాణం మార్చు",
            "Ajustar proporcionalmente",
            "Ajuster en conservant les proportions",
            "Ajustar proporcionalmente",
        },
        ["Disconnected {0} ({1})"] = new[]
        {
            "未接続 {0} ({1})",
            "已断开 {0} ({1})",
            "Getrennt {0} ({1})",
            "कनेक्शन टूट गया {0} ({1})",
            "இணைப்பு துண்டிக்கப்பட்டது {0} ({1})",
            "కనెక్షన్ తెగిపోయింది {0} ({1})",
            "Desconectado {0} ({1})",
            "Déconnecté {0} ({1})",
            "Desconectado {0} ({1})",
        },
        ["NotSupported"] = new[]
        {
            "接触検知非対応",
            "不支持接触检测",
            "Kontakterkennung nicht unterstützt",
            "संपर्क पहचान समर्थित नहीं है",
            "தொடுகை கண்டறிதல் ஆதரிக்கப்படவில்லை",
            "స్పర్శ గుర్తింపుకు మద్దతు లేదు",
            "Detección de contacto no compatible",
            "Détection de contact non prise en charge",
            "Detecção de contato não suportada",
        },
        ["NotSupported2"] = new[]
        {
            "接触検知非対応",
            "不支持接触检测",
            "Kontakterkennung nicht unterstützt",
            "संपर्क पहचान समर्थित नहीं है",
            "தொடுகை கண்டறிதல் ஆதரிக்கப்படவில்லை",
            "స్పర్శ గుర్తింపుకు మద్దతు లేదు",
            "Detección de contacto no compatible",
            "Détection de contact non prise en charge",
            "Detecção de contato não suportada",
        },
        ["NoContact"] = new[]
        {
            "接触なし",
            "未接触",
            "Kein Kontakt",
            "संपर्क नहीं है",
            "தொடுகை இல்லை",
            "స్పర్శ లేదు",
            "Sin contacto",
            "Aucun contact",
            "Sem contato",
        },
        ["Contact"] = new[]
        {
            "接触あり",
            "已接触",
            "Kontakt",
            "संपर्क है",
            "தொடுகை உள்ளது",
            "స్పర్శ ఉంది",
            "Contacto",
            "Contact",
            "Contato",
        },
        ["BPMs @ {0}"] = new[]
        {
            "心拍数: {0} 拍/分",
            "心率：{0} 次/分",
            "Herzfrequenz: {0} Schläge/min",
            "हृदय गति: {0} धड़कन/मिनट",
            "இதயத் துடிப்பு: {0} துடிப்புகள்/நிமிடம்",
            "హృదయ స్పందన: నిమిషానికి {0} సార్లు",
            "Frecuencia cardíaca: {0} latidos/min",
            "Fréquence cardiaque : {0} battements/min",
            "Frequência cardíaca: {0} batimentos/min",
        },
        ["Unable to load background image file \"{0}\" due to error: {1}"] = new[]
        {
            "背景画像ファイル「{0}」を読み込めません。エラー: {1}",
            "无法加载背景图片文件“{0}”，错误：{1}",
            "Die Hintergrundbilddatei „{0}“ konnte nicht geladen werden. Fehler: {1}",
            "पृष्ठभूमि की छवि की फ़ाइल \"{0}\" लोड नहीं हो सकी। त्रुटि: {1}",
            "பின்னணிப் படக் கோப்பு \"{0}\"-ஐ ஏற்ற முடியவில்லை. பிழை: {1}",
            "నేపథ్య చిత్ర ఫైల్ \"{0}\"ను లోడ్ చేయలేకపోయాము. లోపం: {1}",
            "No se pudo cargar el archivo de imagen de fondo \"{0}\". Error: {1}",
            "Impossible de charger le fichier d’image d’arrière-plan « {0} ». Erreur : {1}",
            "Não foi possível carregar o arquivo de imagem de fundo \"{0}\". Erro: {1}",
        },
        ["CSV Files"] = new[]
        {
            "CSVファイル",
            "CSV 文件",
            "CSV-Dateien",
            "CSV फ़ाइलें",
            "CSV கோப்புகள்",
            "CSV ఫైళ్లు",
            "Archivos CSV",
            "Fichiers CSV",
            "Arquivos CSV",
        },
        ["Text Files"] = new[]
        {
            "テキストファイル",
            "文本文件",
            "Textdateien",
            "टेक्स्ट फ़ाइलें",
            "உரைக் கோப்புகள்",
            "వచన ఫైళ్లు",
            "Archivos de texto",
            "Fichiers texte",
            "Arquivos de texto",
        },
        ["Image files"] = new[]
        {
            "画像ファイル",
            "图片文件",
            "Bilddateien",
            "छवि फ़ाइलें",
            "படக் கோப்புகள்",
            "చిత్ర ఫైళ్లు",
            "Archivos de imagen",
            "Fichiers image",
            "Arquivos de imagem",
        },
        ["All files (*.*)"] = new[]
        {
            "すべてのファイル (*.*)",
            "所有文件 (*.*)",
            "Alle Dateien (*.*)",
            "सभी फ़ाइलें (*.*)",
            "அனைத்துக் கோப்புகளும் (*.*)",
            "అన్ని ఫైళ్లు (*.*)",
            "Todos los archivos (*.*)",
            "Tous les fichiers (*.*)",
            "Todos os arquivos (*.*)",
        },
        ["Keep the font size fixed when resizing the window. Set the size in the font dialog."] = new[]
        {
            "ウィンドウのサイズを変更しても文字サイズを固定します。サイズはフォントのダイアログで設定できます。",
            "调整窗口大小时保持字体大小不变。可在字体对话框中设置大小。",
            "Die Schriftgröße bleibt beim Ändern der Fenstergröße gleich. Sie lässt sich im Schriftdialog einstellen.",
            "विंडो का आकार बदलते समय फ़ॉन्ट का आकार स्थिर रखें। फ़ॉन्ट डायलॉग में आकार सेट करें।",
            "சாளரத்தின் அளவை மாற்றும்போது எழுத்துரு அளவை மாறாமல் வைத்திரு. எழுத்துரு உரையாடல் பெட்டியில் அளவை அமைக்கலாம்.",
            "విండో పరిమాణాన్ని మార్చేటప్పుడు ఫాంట్ పరిమాణాన్ని స్థిరంగా ఉంచండి. ఫాంట్ డైలాగ్‌లో పరిమాణాన్ని సెట్ చేయండి.",
            "Mantener fijo el tamaño de la fuente al cambiar el tamaño de la ventana. Establecer el tamaño en el cuadro de diálogo de fuente.",
            "Conserver la taille de la police lors du redimensionnement de la fenêtre. La taille se règle dans la boîte de dialogue de police.",
            "Manter o tamanho da fonte fixo ao redimensionar a janela. Defina o tamanho na caixa de diálogo de fonte.",
        },
        ["Write the latest heart rate to this file, replacing its previous contents. Supports date tokens such as %date:MM-dd-yyyy%."] = new[]
        {
            "最新の心拍数をこのファイルに上書きします。%date:MM-dd-yyyy% などの日付トークンを使用できます。",
            "将最新心率写入此文件，覆盖原有内容。支持 %date:MM-dd-yyyy% 等日期标记。",
            "Schreibt die aktuelle Herzfrequenz in diese Datei und ersetzt den bisherigen Inhalt. Unterstützt Datumsplatzhalter wie %date:MM-dd-yyyy%.",
            "इस फ़ाइल की पिछली सामग्री को बदलकर नवीनतम हृदय गति लिखें। %date:MM-dd-yyyy% जैसे तारीख टोकन समर्थित हैं।",
            "இந்தக் கோப்பின் முந்தைய உள்ளடக்கத்தை மாற்றி, அண்மைய இதயத் துடிப்பை எழுது. %date:MM-dd-yyyy% போன்ற தேதிக் குறியீடுகளைப் பயன்படுத்தலாம்.",
            "ఈ ఫైల్‌లోని పాత సమాచారాన్ని భర్తీ చేస్తూ తాజా హృదయ స్పందనను రాయండి. %date:MM-dd-yyyy% వంటి తేదీ టోకెన్‌లకు మద్దతు ఉంది.",
            "Escribir la frecuencia cardíaca más reciente en este archivo, reemplazando su contenido anterior. Admite marcadores de fecha como %date:MM-dd-yyyy%.",
            "Écrire la fréquence cardiaque la plus récente dans ce fichier en remplaçant son contenu précédent. Accepte les marqueurs de date comme %date:MM-dd-yyyy%.",
            "Gravar a frequência cardíaca mais recente neste arquivo, substituindo o conteúdo anterior. Aceita marcadores de data como %date:MM-dd-yyyy%.",
        },
        ["Write recorded readings to this file. Leave empty to disable file logging. Supports %date% or a custom format such as %date:MM-dd-yyyy%."] = new[]
        {
            "測定値をこのファイルに記録します。空欄にすると記録を無効にします。%date% や %date:MM-dd-yyyy% などの書式を使用できます。",
            "将测量数据记录到此文件。留空可禁用文件记录。支持 %date% 或 %date:MM-dd-yyyy% 等自定义格式。",
            "Schreibt Messwerte in diese Datei. Leer lassen, um die Dateiaufzeichnung zu deaktivieren. Unterstützt %date% oder ein eigenes Format wie %date:MM-dd-yyyy%.",
            "रिकॉर्ड की गई रीडिंग इस फ़ाइल में लिखें। फ़ाइल में रिकॉर्डिंग बंद करने के लिए खाली छोड़ें। %date% या %date:MM-dd-yyyy% जैसा कस्टम प्रारूप समर्थित है।",
            "பதிவுசெய்த அளவீடுகளை இந்தக் கோப்பில் எழுது. கோப்பில் பதிவு செய்வதை நிறுத்த காலியாக விடவும். %date% அல்லது %date:MM-dd-yyyy% போன்ற தனிப்பயன் வடிவத்தைப் பயன்படுத்தலாம்.",
            "నమోదైన కొలతలను ఈ ఫైల్‌లో రాయండి. ఫైల్‌లో నమోదు చేయడాన్ని నిలిపివేయడానికి ఖాళీగా వదలండి. %date% లేదా %date:MM-dd-yyyy% వంటి అనుకూల ఆకృతికి మద్దతు ఉంది.",
            "Escribir las mediciones registradas en este archivo. Dejar vacío para desactivar el registro en archivo. Admite %date% o un formato personalizado como %date:MM-dd-yyyy%.",
            "Écrire les mesures enregistrées dans ce fichier. Laisser vide pour désactiver l’enregistrement dans un fichier. Accepte %date% ou un format personnalisé comme %date:MM-dd-yyyy%.",
            "Gravar as medições registradas neste arquivo. Deixe em branco para desativar o registro em arquivo. Aceita %date% ou um formato personalizado como %date:MM-dd-yyyy%.",
        },
        ["Write RR intervals in milliseconds in IBI format. Supports date tokens such as %date:MM-dd-yyyy%."] = new[]
        {
            "RR間隔をミリ秒単位でIBI形式に記録します。%date:MM-dd-yyyy% などの日付トークンを使用できます。",
            "以 IBI 格式记录 RR 间期，单位为毫秒。支持 %date:MM-dd-yyyy% 等日期标记。",
            "Schreibt RR-Intervalle in Millisekunden im IBI-Format. Unterstützt Datumsplatzhalter wie %date:MM-dd-yyyy%.",
            "RR अंतराल को मिलीसेकंड में IBI प्रारूप में लिखें। %date:MM-dd-yyyy% जैसे तारीख टोकन समर्थित हैं।",
            "RR இடைவெளிகளை மில்லிவினாடிகளில் IBI வடிவத்தில் எழுது. %date:MM-dd-yyyy% போன்ற தேதிக் குறியீடுகளைப் பயன்படுத்தலாம்.",
            "RR విరామాలను మిల్లీసెకన్లలో IBI ఆకృతిలో రాయండి. %date:MM-dd-yyyy% వంటి తేదీ టోకెన్‌లకు మద్దతు ఉంది.",
            "Escribir los intervalos RR en milisegundos en formato IBI. Admite marcadores de fecha como %date:MM-dd-yyyy%.",
            "Écrire les intervalles RR en millisecondes au format IBI. Accepte les marqueurs de date comme %date:MM-dd-yyyy%.",
            "Gravar os intervalos RR em milissegundos no formato IBI. Aceita marcadores de data como %date:MM-dd-yyyy%.",
        },
    };

    internal static string Get(string english, CultureInfo culture = null)
    {
        culture ??= _selectedCulture ?? CultureInfo.CurrentUICulture;
        for (; !culture.Equals(CultureInfo.InvariantCulture); culture = culture.Parent)
        {
            var column = culture.Name switch
            {
                "ja" => 0,
                "zh-Hans" => 1,
                "de" => 2,
                "hi" => 3,
                "ta" => 4,
                "te" => 5,
                "es" => 6,
                "fr" => 7,
                "pt-BR" => 8,
                _ => -1
            };
            if (column >= 0 && Translations.TryGetValue(english, out var values))
                return values[column];
        }
        return english;
    }

    internal static string Format(string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(english), args);

    internal static string FileFilter(string filter)
    {
        var parts = filter.Split('|');
        // Translate descriptions only; wildcard patterns are machine-readable.
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Get(parts[i]);
        return string.Join("|", parts);
    }
}
