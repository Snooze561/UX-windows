# UX-windows

Une application Windows minimaliste pour le double écran :
fond d'écran animé économe en ressources, et un panneau utilitaire sur l'écran de gauche
(horloge, températures et consommation du CPU et du GPU, Apple Music).

Le détail est dans le [cahier des charges](docs/CAHIER_DES_CHARGES.md).

## Essayer l'application (sans rien installer)

1. Ouvre l'onglet **Actions** du dépôt sur GitHub.
2. Clique sur la dernière exécution **Compilation** marquée d'une coche verte.
3. En bas de la page, télécharge **UXWindows** (un fichier `.zip`).
4. Décompresse-le et lance `UXWindows.exe`.
   Windows peut afficher « Windows a protégé votre ordinateur » : clique sur **Informations complémentaires**, puis **Exécuter quand même**. C'est normal pour une app pas encore signée.

Une icône apparaît à côté de l'horloge de la barre des tâches : clic droit pour masquer le panneau ou quitter.

## Avancement

- [x] Étape 1 : cadrage et squelette (panneau de gauche avec horloge, icône de notification)
- [ ] Étape 2 : fond d'écran animé avec pause automatique en plein écran
- [ ] Étape 3 : widgets CPU / GPU (températures, watts) et Apple Music
- [ ] Étape 4 : réglages, lancement au démarrage, installateur
