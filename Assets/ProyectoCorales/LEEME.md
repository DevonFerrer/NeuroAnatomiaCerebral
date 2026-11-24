# Anatomía RA — proyectoCorales

App Android (Unity 6 + URP) que muestra el modelo del encéfalo (Cerebro.glb) en realidad aumentada
con ARCore, y en vista 3D cuando no hay cámara o el equipo no es compatible.

## Primera vez
1. Abre el proyecto en Unity. El Package Manager instalará glTFast, AR Foundation y ARCore.
2. Al terminar de compilar se ejecuta solo **ProyectoCorales ▸ Configurar proyecto (Android + RA)**:
   crea `Escenas/AnatomiaRA.unity`, el prefab interactivo del cerebro, materiales, ARCore y Player Settings.
   Si no se ejecuta, lánzalo desde ese menú.
3. Pulsa **Play** para probar en el editor (vista 3D: arrastra con el mouse para rotar, rueda para zoom, clic para seleccionar).
4. Para el celular: File ▸ Build Profiles ▸ Android ▸ Switch Platform ▸ Build And Run.

## Estructura
- `Scripts/Core/AppController.cs` — pantallas y flujo (Carga, Inicio, Instrucciones, Escaneo, Error, Visor RA/3D, Capas, Info, Cómo usar, Acerca de, Cámara denegada).
- `Scripts/Core/ModelController.cs` — capas, transparencia, resaltado, aislar, rotar/zoom.
- `Scripts/Core/StructureCatalog.cs` — textos de cada estructura (edítalos aquí).
- `Scripts/Core/ARBridge.cs` — AR Foundation (detección de planos y colocación).
- `Scripts/UI/UIKit.cs` — tema (colores) y componentes de UI generados por código.
- `Editor/ProyectoCoralesSetup.cs` — configuración automática.
