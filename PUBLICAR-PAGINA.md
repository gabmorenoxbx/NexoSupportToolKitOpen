# Nexo Support Toolkit — página web

Sitio estático en español para presentar Nexo Support Toolkit. No requiere Node, servidor ni dependencias externas: abrí `index.html` para previsualizarlo.

## Publicar en GitHub Pages

1. Copiá `index.html`, `favicon.ico` y la carpeta `assets` al repositorio público (o a la carpeta que quieras usar para Pages).
2. En GitHub, abrí **Settings → Pages**.
3. Elegí la rama `master` y la carpeta `/ (root)` como origen, y guardá.
4. GitHub mostrará la dirección pública del sitio al terminar la publicación.

Los botones apuntan al repositorio `gabmorenoxbx/NexoSupportToolKitOpen` y a su sección de Releases. Publicá allí el instalador como un Release para que aparezca disponible en la página.

## Publicar el instalador para que funcione el botón de descarga

El botón descarga el instalador desde el asset con este nombre en la última Release:
`NexoSupportToolKit-Setup-1.0.0-x64.exe`.

En GitHub, abrí **Releases → Draft a new release**, creá una etiqueta (por ejemplo `v1.0.0`), adjuntá el archivo `.exe` y publicá la Release. El enlace de descarga de la página quedará activo al publicarla.
