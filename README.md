# YouTube letöltő

Windowsos grafikus YouTube-letöltő magyar felülettel.

## Forráskód

A [VideoLetolto.cs](VideoLetolto.cs) az alkalmazás fő forrásfájlja. A korábbi helyi, hordozható változat futtatható fájljai és segédprogramjai nem részei ennek a forrásfeltöltésnek.

## Helyi futtatás

A kész hordozható csomagban az alkalmazás mellé szükséges a `yt-dlp.exe` és a `node.exe`; ezek külön beszerzendő külső programok. A helyi példány indításához ezek a fájlok együtt vannak a letöltővel.

A letöltések a felhasználó által kiválasztott mappába kerülnek. A letöltési napló, gyorsítótár és videófájlok nincsenek a GitHubra feltöltve.

## Külső összetevők

- yt-dlp: https://github.com/yt-dlp/yt-dlp
- Node.js: https://nodejs.org/
