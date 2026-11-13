# Real-Time Decision Making in Video Games

## 1. Présentation du projet

Ce projet est réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l'EPF pour l'année universitaire 2026–2027.

L'objectif est de développer une intelligence artificielle pour l'armée rouge dans un jeu de stratégie en temps réel de type RTS Wargame, développé avec Unity 3D et C#.

L'armée rouge doit être capable de vaincre l'armée verte contrôlée par l'IA par défaut. Pour atteindre cet objectif, nous améliorons les arbres de comportement des drones et des tourelles d'artillerie, ainsi que la coordination globale des unités.

Le projet comprend également le développement d'une fonctionnalité supplémentaire afin de différencier notre solution.

---

## 2. Objectifs

Les objectifs principaux sont les suivants :

- Développer une IA rouge plus performante que l'IA verte par défaut.
- Améliorer la sélection et la répartition des cibles ennemies.
- Optimiser la coordination entre les drones et les tourelles d'artillerie.
- Réduire les déplacements inutiles et les attaques inefficaces.
- Concevoir des arbres de comportement adaptés aux différents types d'unités.
- Implémenter une fonctionnalité supplémentaire.
- Évaluer les performances de l'IA à partir de simulations et de données statistiques.

---

## 3. État initial et contraintes

Le jeu met en scène deux armées qui s'affrontent sur une carte de 60 × 60 mètres comprenant une plaine, des terrasses surélevées et différents obstacles.

Chaque armée dispose principalement de deux types d'unités :

- **Drones :** unités mobiles capables de se déplacer vers une cible, de s'orienter et de tirer des missiles.
- **Tourelles d'artillerie :** unités statiques capables de s'orienter et de tirer des grenades à trajectoire balistique.

### Effectifs initiaux

| Unité | Armée rouge | Armée verte |
|---|---:|---:|
| Drones | 13 | 19 |
| Tourelles d'artillerie | 6 | 9 |

L'armée rouge est donc en infériorité numérique.

Notre stratégie consiste à compenser ce désavantage grâce à une meilleure prise de décision, une meilleure coordination et une utilisation plus efficace des unités.

### Contraintes techniques

- Le comportement de l'armée verte ne doit pas être modifié.
- Les caractéristiques des unités définies dans l'annexe A du sujet ne doivent pas être modifiées.
- Le développement doit respecter l'architecture existante du projet Unity.
- Les modifications doivent être testées afin de vérifier leur impact sur les performances de l'armée rouge.
- Les missiles des drones conservent une trajectoire rectiligne.
- Les améliorations doivent principalement porter sur la prise de décision et le comportement de l'IA.

---

## 4. Stratégie générale

Notre stratégie repose sur trois principes :

1. **Concentration des forces :** concentrer les attaques sur des cibles prioritaires plutôt que de disperser inutilement les tirs.
2. **Coordination des unités :** améliorer la coopération entre les drones, les tourelles et le gestionnaire de l'armée.
3. **Adaptation au champ de bataille :** utiliser les informations disponibles sur les positions, les trajectoires, les points de vie et l'état du combat pour ajuster les décisions.

L'objectif est de maximiser l'efficacité de chaque unité sans modifier ses caractéristiques de base.

---

## 5. Stratégies tactiques

### 5.1. Priorisation des cibles

La sélection des cibles ne repose plus uniquement sur un choix simple ou aléatoire.

Pour l'artillerie rouge, une sélection tactique a été implémentée.

Les critères actuellement utilisés comprennent notamment :

- Le type d'unité ennemie.
- La distance de la cible.
- Ses points de vie restants.
- Les dégâts des projectiles déjà en vol vers cette cible.
- La vitesse des drones ennemis.
- La présence de plusieurs drones dans une même zone.

Les tourelles ennemies sont actuellement considérées comme des cibles prioritaires.

Lorsqu'aucune tourelle pertinente n'est disponible, l'artillerie peut sélectionner des drones en privilégiant les unités lentes ou regroupées.

---

### 5.2. Comportement des drones rouges

Une première logique spécifique aux drones rouges a été intégrée.

Des tâches spécifiques permettent notamment :

- De demander une cible à `ArmyManagerRed`.
- De sélectionner une cible ennemie.
- De se déplacer vers cette cible.
- De s'orienter.
- De tirer.
- D'enregistrer les dégâts potentiels des missiles déjà en vol.

Le comportement actuel reste en cours d'évaluation.

Les simulations permettent désormais d'identifier plusieurs problèmes concrets concernant la mobilité, la précision et l'activité des drones rouges.

Ces observations sont détaillées dans la section **Analyse des problèmes actuels**.

---

### 5.3. Optimisation des tourelles d'artillerie

L'intelligence artificielle des tourelles d'artillerie rouges a été améliorée.

Le comportement actuel utilise une nouvelle logique de sélection des cibles ainsi qu'une coordination des tirs entre les différentes tourelles.

Les améliorations implémentées comprennent :

- La priorité donnée aux tourelles ennemies.
- La prise en compte des points de vie de la cible.
- La prise en compte des projectiles déjà en vol.
- L'estimation des dégâts futurs sur une cible.
- La réduction du **overkill**.
- Le changement de cible lorsqu'un nombre suffisant de projectiles est déjà en route pour détruire une cible.
- La priorité donnée aux drones lents ou presque immobiles.
- La détection des groupes de drones.
- L'exploitation des dégâts de zone des grenades d'artillerie.
- La réévaluation régulière de la cible.

Cette stratégie permet de mieux répartir les tirs entre plusieurs cibles et de limiter le gaspillage de projectiles.

---

### 5.4. Gestion des projectiles déjà en vol

Une logique spécifique a été ajoutée dans `ArmyManagerRed` afin de suivre les dégâts des projectiles déjà tirés par l'armée rouge.

Les tirs d'artillerie ainsi que les missiles des drones rouges peuvent être enregistrés comme des dégâts potentiels en cours.

Le système estime ensuite la santé future de la cible :

```text
Santé estimée
=
Santé actuelle
-
Dégâts des projectiles déjà en vol
```

Si les dégâts prévus sont suffisants pour détruire une cible, celle-ci peut être considérée comme déjà couverte par les tirs en cours.

Les tirs enregistrés sont supprimés après un délai correspondant approximativement à leur durée de vol.

Ainsi, si un projectile rate sa cible ou si celle-ci survit, elle peut redevenir sélectionnable.

---

### 5.5. Ciblage des drones par l'artillerie

Les drones en mouvement rapide sont difficiles à toucher avec l'artillerie à cause du temps de vol des projectiles.

L'artillerie rouge prend actuellement en compte :

- La vitesse des drones.
- La distance.
- La présence de plusieurs drones proches les uns des autres.
- La disponibilité des tourelles ennemies comme cibles prioritaires.

Cette logique permet à l'artillerie de sélectionner des cibles présentant un meilleur potentiel d'efficacité.

---

### 5.6. Coordination globale de l'armée

Le gestionnaire `ArmyManagerRed` constitue le centre de décision de l'armée rouge.

Il peut exploiter les informations disponibles concernant :

- Les positions des unités alliées et ennemies.
- Les points de vie des unités.
- Les dégâts des projectiles déjà en vol.
- La vitesse de certaines unités.
- La proximité entre plusieurs cibles.

Une première partie de cette coordination est déjà utilisée afin de :

- Répartir les cibles d'artillerie.
- Suivre les dégâts déjà en cours.
- Limiter certains tirs redondants.
- Adapter le choix de cible en fonction de l'évolution du combat.
- Fournir une interface commune de demande de cible aux unités rouges.

La coordination globale entre les différents types d'unités reste en cours d'évaluation.

---

## 6. Architecture de l'intelligence artificielle

### 6.1. `ArmyManagerRed`

`ArmyManagerRed` centralise une partie des décisions tactiques de l'armée rouge.

Les fonctionnalités actuellement implémentées comprennent :

- Évaluation des cibles disponibles.
- Sélection tactique des cibles.
- Prise en compte des points de vie.
- Suivi des dégâts déjà en vol.
- Estimation des dégâts futurs.
- Réduction du overkill.
- Répartition des tirs entre plusieurs cibles.
- Détection de drones lents.
- Détection de groupes de drones.
- Gestion des dégâts potentiels des missiles des drones.
- Interface commune `RequestTarget` pour plusieurs types d'unités rouges.

---

### 6.2. `DroneRedBehavior`

Le Behavior Tree des drones rouges est actuellement en cours d'optimisation.

Des tâches spécifiques ont été ajoutées :

- `SelectRedDroneTarget`
- `RedDroneShoot`

Le comportement général actuel repose sur :

```text
Entry
└── Repeater
    └── Sequence
        ├── Select Red Drone Target
        ├── Déplacement
        ├── Orientation
        ├── Red Drone Shoot
        └── Wait
```

Les simulations montrent encore plusieurs comportements indésirables qui devront être étudiés par l'équipe responsable de cette partie.

---

### 6.3. `TurretRedBehavior`

Le Behavior Tree des tourelles rouges a été modifié.

Sa structure principale est actuellement :

```text
Entry
└── Repeater
    └── Sequence
        ├── Select Red Turret Target
        ├── Turret Seek Target
        ├── Red Turret Shoot
        └── Wait
```

Deux tâches principales sont utilisées :

- `SelectRedTurretTarget` : sélection tactique de la cible.
- `RedTurretShoot` : tir de la tourelle et enregistrement du projectile en cours.

Les arbres de comportement sont développés avec Behavior Designer.

---

## 7. Fonctionnalité supplémentaire

Une fonctionnalité supplémentaire doit être développée afin d'apporter une valeur ajoutée au projet.

### Proposition actuelle : unité mobile de réparation

Une unité de soutien est envisagée afin de restaurer les points de vie des unités alliées endommagées.

Une première structure permettant d'identifier une future unité de type `Medic` existe déjà dans `ArmyManagerRed`.

La fonctionnalité complète reste cependant à concevoir, intégrer et tester.

---

## 8. Système global de statistiques

Un système global de statistiques de combat a été développé afin d'analyser objectivement les performances des deux armées.

Le script principal est :

```text
BattleStatsManager.cs
```

Le système collecte automatiquement les données des armées RED et GREEN pendant chaque bataille.

### Statistiques des drones

Pour chaque armée, le système enregistre :

- Le nombre de missiles tirés.
- Le nombre de missiles ayant réellement touché une unité ennemie.
- Le taux de précision des missiles.

Le taux de précision est calculé par :

```text
Précision
=
Nombre de missiles ayant touché une cible
/
Nombre total de missiles tirés
×
100
```

---

### Statistiques de l'artillerie

Pour chaque armée, le système enregistre :

- Le nombre de tirs d'artillerie.
- Le nombre de tirs ayant touché au moins une unité.
- Le taux de précision.
- Le nombre total d'unités touchées par les explosions.
- Le nombre moyen d'unités touchées par tir.

Cette dernière mesure permet notamment d'évaluer l'efficacité des dégâts de zone.

---

### Résultats de fin de bataille

Lorsqu'une armée est entièrement détruite, le système affiche automatiquement un panneau de résultats contenant :

- L'armée gagnante.
- La durée de la bataille.
- Le nombre de drones survivants.
- Le nombre de tourelles survivantes.
- Les points de vie cumulés restants.
- Les statistiques des missiles.
- Les statistiques de l'artillerie.

L'interface sépare les résultats de l'armée rouge et de l'armée verte afin de faciliter la comparaison.

Les mêmes informations sont également disponibles dans la Console Unity pour faciliter le débogage.

---

## 9. État actuel du développement

### Implémenté

- Analyse du projet Unity existant.
- Modification de `TurretRedBehavior`.
- Nouvelle sélection tactique des cibles pour l'artillerie rouge.
- Priorité aux tourelles ennemies.
- Prise en compte des points de vie des cibles.
- Suivi des projectiles déjà en vol.
- Estimation des dégâts futurs.
- Réduction du overkill.
- Changement automatique de cible lorsqu'une cible est déjà suffisamment couverte.
- Prise en compte des drones lents.
- Détection des groupes de drones.
- Exploitation des dégâts de zone.
- Première intégration de la logique spécifique des drones rouges.
- Ajout de `SelectRedDroneTarget`.
- Ajout de `RedDroneShoot`.
- Prise en compte des dégâts potentiels des missiles rouges.
- Développement de `BattleStatsManager`.
- Statistiques globales des drones RED et GREEN.
- Statistiques globales de l'artillerie RED et GREEN.
- Calcul automatique des taux de précision.
- Calcul de l'efficacité des dégâts de zone.
- Calcul des unités survivantes.
- Calcul des points de vie restants.
- Enregistrement de la durée de la bataille.
- Détection du gagnant.
- Affichage automatique d'un panneau de résultats en fin de bataille.

### Problèmes actuellement observés / travaux restant à étudier

- Comportement de `DroneRedBehavior` encore instable dans certaines situations.
- Regroupement important de plusieurs drones rouges dans une même zone.
- Différence de précision observée entre les missiles tirés par un même drone.
- Certains drones rouges restent parfois immobiles ou inactifs pendant une partie du combat.
- L'armée rouge reste très vulnérable aux dégâts de zone de l'artillerie verte.
- La coordination globale entre drones et tourelles doit encore être évaluée.
- La fonctionnalité supplémentaire reste à finaliser.
- Le système complet doit encore être validé sur plusieurs batailles.
- Les 20 simulations finales restent à effectuer.
- L'analyse statistique finale reste à réaliser.
- La vidéo et la livraison finale restent à préparer.

---

## 10. Plan de développement

| Étape | Travail | État |
|---|---|---|
| 1 | Analyse du projet Unity existant | Terminé |
| 2 | Premiers tests | Réalisé |
| 3 | Développement de `ArmyManagerRed` | En cours |
| 4 | Développement de `DroneRedBehavior` | En cours |
| 5 | Optimisation de `TurretRedBehavior` | Implémentée |
| 6 | Système global de statistiques | Implémenté |
| 7 | Interface de résultats | Implémentée |
| 8 | Fonctionnalité supplémentaire | À poursuivre |
| 9 | Intégration et débogage | En cours |
| 10 | Simulations finales | À réaliser |
| 11 | Analyse finale | À réaliser |
| 12 | Préparation de la livraison | À réaliser |

---

## 11. Stratégie de test et critères d'évaluation

Les performances sont évaluées à partir de simulations contre l'IA verte par défaut.

Conformément au sujet, **20 batailles valides** devront être simulées pour l'évaluation finale.

Les indicateurs principaux sont :

- **Taux de victoire**
- **Nombre de drones survivants**
- **Nombre de tourelles survivantes**
- **Points de vie cumulés restants**
- **Durée de la bataille**

Le système développé permet également de suivre :

- Le nombre de missiles tirés.
- Le nombre de missiles ayant touché une cible.
- La précision des drones.
- Le nombre de tirs d'artillerie.
- Le nombre de tirs d'artillerie réussis.
- La précision de l'artillerie.
- Le nombre total d'unités touchées par les explosions.
- Le nombre moyen d'unités touchées par tir d'artillerie.

Ces indicateurs sont collectés automatiquement par `BattleStatsManager` et affichés à la fin de chaque bataille.

Les situations de blocage (*deadlock*) identifiées comme non comptabilisables devront être relancées conformément aux consignes.

---

## 12. Analyse des problèmes actuels

Les premières simulations permettent d'identifier plusieurs comportements qui limitent actuellement les performances de l'armée rouge.

Cette section décrit uniquement les problèmes observés. Les choix d'implémentation permettant de les corriger restent à définir par les membres responsables des différentes parties du projet.

### 12.1. Regroupement excessif des drones rouges

Plusieurs drones rouges ont tendance à se déplacer vers les mêmes zones du terrain.

Ils peuvent ainsi se retrouver très proches les uns des autres pendant le combat.

Ce comportement augmente fortement leur vulnérabilité face aux attaques possédant des dégâts de zone.

Les simulations montrent notamment que l'artillerie verte peut toucher plusieurs unités rouges avec une même explosion lorsque celles-ci sont regroupées.

---

### 12.2. Différence de précision entre les missiles d'un drone

Chaque drone tire plusieurs missiles depuis différents points de lancement.

Pendant les tests, il a été observé qu'un missile peut être correctement orienté vers la cible alors qu'un autre missile tiré par le même drone passe à côté.

Ce comportement réduit l'efficacité réelle des attaques des drones rouges.

Les statistiques globales montrent également que la précision des drones rouges reste inférieure à celle des drones verts dans certaines batailles.

---

### 12.3. Drones temporairement inactifs

Certains drones rouges restent parfois immobiles pendant une partie du combat.

Ils peuvent alors ne plus se déplacer et ne plus attaquer pendant plusieurs secondes.

Ce phénomène réduit fortement la puissance effective de l'armée rouge, qui possède déjà moins de drones que l'armée verte au début de la bataille.

La cause exacte de ce comportement doit encore être identifiée.

---

### 12.4. Vulnérabilité aux dégâts de zone

Les simulations montrent que les pertes rouges peuvent être très rapides lorsque plusieurs drones se trouvent dans une même zone.

Même lorsque l'artillerie verte possède une précision inférieure à celle de l'artillerie rouge, ses explosions peuvent provoquer des dégâts importants sur plusieurs unités simultanément.

Cette situation contribue à réduire rapidement le nombre d'unités rouges actives.

---

### 12.5. Déséquilibre entre précision et volume de feu

Les premières simulations montrent que l'artillerie rouge peut obtenir une précision supérieure à celle de l'artillerie verte.

Exemple observé lors d'une bataille :

```text
RED Artillery Accuracy   : environ 74 %
GREEN Artillery Accuracy : environ 54 %
```

Cependant, l'armée verte peut effectuer un nombre de tirs nettement supérieur grâce à son avantage numérique et à la survie plus longue de ses unités.

Une meilleure précision individuelle ne garantit donc pas encore une meilleure performance globale de l'armée rouge.

---

### 12.6. Faible survie globale de l'armée rouge

Lors de certaines simulations, l'armée rouge est entièrement détruite alors que l'armée verte conserve encore une grande partie de ses unités.

Un exemple observé donne approximativement :

```text
RED
Drones survivants : 0
Tourelles survivantes : 0
Santé restante : 0

GREEN
Drones survivants : 18
Tourelles survivantes : 5
Santé restante : plus de 1000
```

Ces résultats montrent que plusieurs problèmes de comportement restent présents malgré l'amélioration de l'efficacité de l'artillerie rouge.

---

## 13. Répartition du travail

Le projet est réalisé par une équipe de **trois étudiants**.

| Membre | Responsabilités principales |
|---|---|
| **LIU Yao** | `ArmyManagerRed`, stratégie globale, allocation des cibles et coordination de l'armée |
| **Ruofan** | `DroneRedBehavior`, sélection des cibles et logique de combat des drones rouges |
| **CHENG Zihao** | `TurretRedBehavior`, logique tactique de l'artillerie rouge, réduction du overkill, instrumentation et système global de statistiques |

### Contributions de CHENG Zihao

Les éléments suivants ont notamment été réalisés :

- Modification et optimisation de `TurretRedBehavior`.
- Développement de la sélection tactique des cibles d'artillerie.
- Priorisation des tourelles ennemies.
- Prise en compte des points de vie des cibles.
- Prise en compte des dégâts déjà en vol.
- Réduction du overkill entre plusieurs tourelles rouges.
- Changement automatique de cible lorsqu'une cible est déjà suffisamment couverte.
- Prise en compte des drones lents.
- Détection des groupes de drones.
- Exploitation des dégâts de zone de l'artillerie.
- Participation à l'intégration de la logique d'artillerie avec `ArmyManagerRed`.
- Développement de `BattleStatsManager`.
- Mise en place d'un système global de statistiques pour les deux armées.
- Instrumentation de `Drone.cs`.
- Instrumentation de `Missile.cs`.
- Instrumentation de `Turret.cs`.
- Instrumentation de `Rocket.cs`.
- Calcul du nombre de tirs et du nombre d'impacts des drones.
- Calcul du taux de précision des missiles.
- Calcul du nombre de tirs et du taux de précision de l'artillerie.
- Calcul du nombre total d'unités touchées par l'artillerie.
- Calcul du nombre moyen d'unités touchées par tir.
- Calcul des unités survivantes.
- Calcul des points de vie restants.
- Enregistrement de la durée du combat.
- Détection et affichage de l'armée gagnante.
- Développement d'une interface de résultats affichée automatiquement à la fin de chaque bataille.
- Mise en place d'outils permettant d'identifier objectivement les problèmes actuels à partir des résultats de combat.

L'intégration, les tests, la validation et le débogage restent réalisés collectivement par l'équipe.

---

## 14. Fichiers principaux

### `ArmyManagerRed.cs`

Contient notamment :

- La logique de sélection tactique des cibles.
- Le suivi des dégâts en cours.
- L'estimation des dégâts futurs.
- La réduction du overkill.
- La sélection des drones lents et des groupes de drones.
- La logique de sélection des cibles des drones rouges.
- Une interface de demande de cible commune aux différents types d'unités rouges.

### `BattleStatsManager.cs`

Centralise les statistiques de combat.

Il permet notamment :

- De compter les tirs.
- De compter les impacts.
- De calculer les taux de précision.
- De mesurer l'efficacité des dégâts de zone.
- De compter les unités survivantes.
- De calculer les points de vie restants.
- D'enregistrer la durée du combat.
- D'identifier le gagnant.
- D'afficher automatiquement les résultats.

### `SelectRedTurretTarget.cs`

Tâche Behavior Designer utilisée pour demander à `ArmyManagerRed` la meilleure cible disponible pour une tourelle rouge.

### `RedTurretShoot.cs`

Tâche spécifique aux tourelles rouges permettant :

- De tirer sur la cible sélectionnée.
- D'enregistrer les dégâts potentiels du projectile.

### `SelectRedDroneTarget.cs`

Tâche Behavior Designer permettant aux drones rouges de demander une cible à `ArmyManagerRed`.

### `RedDroneShoot.cs`

Tâche spécifique aux drones rouges permettant de tirer sur la cible sélectionnée et d'enregistrer les dégâts potentiels des missiles.

### `TurretRedBehavior.asset`

Behavior Tree utilisé par les tourelles rouges.

### `DroneRedBehavior.asset`

Behavior Tree utilisé par les drones rouges.

---

## 15. Résultats attendus

Le projet vise à obtenir :

- Une armée rouge capable de prendre des décisions tactiques plus cohérentes.
- Une meilleure coordination entre les unités.
- Une réduction des attaques inefficaces.
- Une utilisation plus efficace des projectiles d'artillerie.
- Des arbres de comportement structurés et faciles à maintenir.
- Une fonctionnalité supplémentaire opérationnelle.
- Des résultats statistiques permettant d'évaluer objectivement les améliorations.

Les résultats définitifs devront être confirmés par les simulations finales.

---

## 16. Technologies utilisées

- **Unity 3D** : moteur de jeu.
- **C#** : langage de programmation.
- **Behavior Designer** : conception des arbres de comportement.
- **Git / GitHub** : gestion du code source et collaboration.
- **TextMeshPro** : affichage des résultats.
- **Unity UI** : interface de fin de bataille.
- **Statistiques de simulation** : analyse des performances des deux armées.

---

## 17. Conclusion

Notre approche consiste à améliorer progressivement la prise de décision de l'armée rouge tout en conservant les caractéristiques originales des unités.

La partie concernant les tourelles d'artillerie rouges dispose déjà d'une sélection tactique des cibles, d'un mécanisme de coordination des tirs, d'une estimation des dégâts futurs et d'une réduction du overkill.

Un système global de statistiques permet maintenant de mesurer automatiquement les performances des drones et de l'artillerie des deux armées.

Les premières simulations montrent que l'artillerie rouge peut obtenir une bonne précision, mais plusieurs problèmes restent observables dans le comportement global de l'armée rouge, notamment au niveau des drones.

Les statistiques et les observations de combat permettent désormais d'identifier ces problèmes objectivement. Les solutions techniques correspondantes restent à définir et à implémenter par les membres responsables des différentes parties du projet.

Les résultats définitifs seront évalués à partir des simulations finales sur 20 batailles valides.
