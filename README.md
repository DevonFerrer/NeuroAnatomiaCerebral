# NeuroAnatomía Cerebral — Anatomía RA

App móvil Android (Unity 6.6 + URP) para explorar el **encéfalo humano** en **realidad aumentada** (ARCore) o en vista 3D.

## Funciones
- Escaneo de superficie y colocación del modelo en RA (AR Foundation + ARCore).
- Vista 3D automática si el dispositivo no admite RA o no hay permiso de cámara.
- 9 estructuras con ambos lados: cerebro, cerebelo, tronco encefálico, tálamo, cuerpo calloso, fórnix, comisura anterior, glándula pituitaria y quiasma óptico.
- Tocar una estructura para ver su descripción y relaciones, resaltarla o aislarla.
- Panel de capas, etiquetas flotantes y transparencia del cerebro.
- Gestos: 1 dedo rotar, 2 dedos zoom.

## Requisitos
- Unity **6000.6.8f1** con el módulo **Android Build Support**.
- Paquetes (se instalan solos desde `Packages/manifest.json`): glTFast, AR Foundation, ARCore.

## Uso
1. Abre la carpeta del proyecto en Unity Hub.
2. La escena principal `Assets/ProyectoCorales/Escenas/AnatomiaRA.unity` se abre sola al abrir el proyecto (si ves una escena vacía "Untitled", ábrela desde la ventana Project o con **ProyectoCorales ▸ Abrir escena principal**).
3. **Play** para probar la vista 3D en el editor.
4. **ProyectoCorales ▸ Compilar APK (Android)** genera `Builds/AnatomiaRA.apk`.

## Estructura
- `Assets/ProyectoCorales/Scripts` — lógica de la app y UI generada por código.
- `Assets/ProyectoCorales/Editor` — configuración automática y compilación del APK.
- `Assets/ProyectoCorales/Modelos/Cerebro.glb` — modelo 3D.

Proyecto académico — Ingeniería de Software II, Universidad Nacional del Santa · Equipo InnoMed.
