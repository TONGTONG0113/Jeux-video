# Real-Time Decision Making in Video Games

## 1. Présentation du projet

Ce projet est réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l'EPF pour l'année universitaire 2026–2027.

L'objectif est de développer une intelligence artificielle pour l'armée rouge dans un jeu de stratégie en temps réel de type RTS Wargame, développé avec Unity 3D et C#.

L'armée rouge doit être capable de vaincre l'armée verte contrôlée par l'IA par défaut. Pour atteindre cet objectif, nous allons améliorer les arbres de comportement des drones et des tourelles d'artillerie, ainsi que la coordination globale des unités.

Le projet comprend également le développement d'une fonctionnalité supplémentaire afin de différencier notre solution.

## 2. Objectifs

Les objectifs principaux sont les suivants :

- Développer une IA rouge plus performante que l'IA verte par défaut.
- Améliorer la sélection et la répartition des cibles ennemies.
- Optimiser la coordination entre les drones et les tourelles d'artillerie.
- Réduire les déplacements inutiles et les attaques inefficaces.
- Concevoir des arbres de comportement adaptés aux différents types d'unités.
- Implémenter une fonctionnalité supplémentaire.
- Évaluer les performances de l'IA à partir de simulations et de données statistiques.

## 3. État initial et contraintes

Le jeu met en scène deux armées qui s'affrontent sur une carte de 60 × 60 mètres comprenant une plaine, des terrasses surélevées et différents obstacles.

Chaque armée dispose de deux types d'unités :

- **Drones :** unités mobiles capables de se déplacer vers une cible, de s'orienter et de tirer des missiles.
- **Tourelles d'artillerie :** unités statiques capables de s'orienter et de tirer des grenades à trajectoire balistique.

### Effectifs initiaux

| Unité | Armée rouge | Armée verte |
|---|---:|---:|
| Drones | 13 | 19 |
| Tourelles d'artillerie | 6 | 9 |

L'armée rouge est donc en infériorité numérique. Notre stratégie consiste à compenser ce désavantage grâce à une meilleure prise de décision et à une utilisation plus efficace des unités.

### Contraintes techniques

- Le comportement de l'armée verte ne doit pas être modifié.
- Les caractéristiques des unités définies dans l'annexe A du sujet ne doivent pas être modifiées.
- Le développement doit respecter l'architecture existante du projet Unity.
- Les modifications doivent être testées afin de vérifier leur impact sur les performances de l'armée rouge.

## 4. Stratégie générale

Notre stratégie repose sur trois principes :

1. **Concentration des forces :** concentrer les attaques sur des cibles prioritaires plutôt que de disperser les unités.
2. **Coordination des unités :** améliorer la coopération entre les drones, les tourelles et le gestionnaire de l'armée.
3. **Adaptation au champ de bataille :** utiliser les informations disponibles sur les positions, les trajectoires, les points de vie, les obstacles et le terrain pour ajuster les décisions.

L'objectif est de maximiser l'efficacité de chaque unité sans modifier ses caractéristiques de base.

## 5. Stratégies tactiques

### 5.1. Priorisation des cibles

Le système de décision devra sélectionner les cibles en fonction de critères tactiques, plutôt que de s'appuyer uniquement sur un choix aléatoire.

Les critères envisagés sont :

- Le type d'unité ennemie.
- La menace représentée par la cible.
- Sa distance et son accessibilité.
- Ses points de vie restants.
- Le nombre d'unités alliées qui l'attaquent déjà.
- La pertinence de poursuivre l'attaque ou de changer de cible.

Les tourelles ennemies pourront être considérées comme des cibles prioritaires lorsqu'elles représentent une menace importante. Toutefois, cette priorité devra être évaluée par des simulations afin de déterminer si elle améliore réellement les résultats.

### 5.2. Concentration des attaques des drones

Les drones utiliseront une sélection de cibles plus cohérente afin de limiter la dispersion des tirs.

Le comportement envisagé comprend les étapes suivantes :

1. Vérifier que la cible est valide et toujours active.
2. Évaluer les cibles disponibles.
3. Sélectionner une cible prioritaire.
4. Se déplacer vers la cible et s'orienter correctement.
5. Tirer les missiles lorsque les conditions d'attaque sont réunies.
6. Vérifier l'état de la cible après l'attaque.
7. Choisir une nouvelle cible si nécessaire.

Cette approche vise à réduire les attaques redondantes et les poursuites inutiles.

### 5.3. Optimisation des tourelles d'artillerie

Les tourelles seront améliorées afin de sélectionner des cibles pertinentes et d'utiliser efficacement leurs attaques à dégâts de zone.

Les améliorations envisagées sont :

- Prioriser les unités ennemies les plus menaçantes.
- Vérifier la validité de la cible avant de tirer.
- Évaluer la présence de plusieurs ennemis à proximité d'une zone d'impact potentielle.
- Éviter de conserver une cible devenue invalide.
- Réévaluer la cible après chaque cycle d'attaque.

L'objectif est d'améliorer l'efficacité des tirs tout en conservant les caractéristiques de base des tourelles.

### 5.4. Coordination globale de l'armée

Le gestionnaire `ArmyManagerRed` constituera le centre de décision de l'armée rouge.

Il pourra exploiter les informations disponibles concernant :

- Les positions et les trajectoires des unités alliées et ennemies.
- Les points de vie des unités.
- Les missiles et les grenades présents sur le champ de bataille.
- Les obstacles et les caractéristiques du terrain.

Il sera chargé de contribuer à une répartition cohérente des cibles, de limiter les doublons et de permettre une adaptation des décisions en fonction de l'évolution du combat.

Le gestionnaire ne connaît pas les intentions de l'ennemi. Les décisions devront donc reposer sur les informations observables disponibles.

## 6. Architecture de l'intelligence artificielle

Le développement reposera principalement sur trois composants.

### 6.1. `ArmyManagerRed`

Responsabilités envisagées :

- Centraliser les informations nécessaires à la prise de décision.
- Évaluer et répartir les cibles.
- Éviter, lorsque cela est possible, que trop d'unités attaquent inutilement la même cible.
- Adapter les priorités selon l'état du champ de bataille.

### 6.2. `DroneRedBehavior`

L'arbre de comportement des drones sera conçu autour des étapes suivantes :

- Vérification de l'état du drone.
- Sélection d'une cible.
- Vérification de la validité de la cible.
- Déplacement vers la cible.
- Orientation et attaque.
- Réévaluation de la situation.
- Sélection d'une nouvelle cible si nécessaire.

### 6.3. `TurretRedBehavior`

L'arbre de comportement des tourelles sera conçu autour des étapes suivantes :

- Vérification de l'état de la tourelle.
- Sélection d'une cible prioritaire.
- Vérification de la validité et de l'accessibilité de la cible.
- Orientation vers la cible.
- Tir d'une grenade.
- Réévaluation de la cible après l'attaque.

Les arbres de comportement seront développés avec Behavior Designer. De nouvelles tâches, conditions ou décorateurs pourront être ajoutés si les fonctionnalités existantes ne permettent pas de réaliser les stratégies prévues.

## 7. Fonctionnalité supplémentaire

Une fonctionnalité supplémentaire sera développée afin d'apporter une valeur ajoutée au projet.

### Proposition : unité mobile de réparation

Une unité de soutien pourrait être ajoutée à l'armée rouge afin de restaurer les points de vie des unités alliées endommagées.

Le comportement envisagé comprendrait :

- La détection des unités alliées nécessitant une réparation.
- La sélection d'une unité prioritaire.
- Le déplacement vers cette unité.
- La réparation lorsque les conditions nécessaires sont réunies.
- Le choix d'une nouvelle cible lorsque la réparation est terminée ou impossible.

Cette fonctionnalité est une proposition à valider. Son développement dépendra de la faisabilité technique, du temps disponible et de la compatibilité avec le projet existant.

Une alternative plus simple serait la création d'une zone de réparation dans laquelle les unités alliées pourraient récupérer des points de vie.

## 8. Plan de développement

Le développement sera organisé en plusieurs étapes.

| Étape | Travail prévu | Résultat attendu |
|---|---|---|
| 1 | Analyse du projet Unity existant | Compréhension de l'architecture et des comportements initiaux |
| 2 | Tests de référence | Collecte des premières statistiques de combat |
| 3 | Développement de `ArmyManagerRed` | Amélioration de la sélection et de la répartition des cibles |
| 4 | Optimisation de `DroneRedBehavior` | Amélioration des déplacements et des attaques |
| 5 | Optimisation de `TurretRedBehavior` | Amélioration du choix des cibles et des tirs |
| 6 | Développement de la fonctionnalité supplémentaire | Ajout d'une nouvelle capacité |
| 7 | Intégration et débogage | Vérification du fonctionnement de l'ensemble |
| 8 | Simulations et analyse | Comparaison des performances et ajustement des stratégies |
| 9 | Préparation de la livraison | Documentation, statistiques et vidéo de démonstration |

## 9. Stratégie de test et critères d'évaluation

Les performances seront évaluées à partir de simulations contre l'IA verte par défaut.

Conformément au sujet, **20 batailles valides** devront être simulées pour l'évaluation finale.

Les indicateurs suivis seront :

- **Taux de victoire :** proportion de batailles remportées par l'armée rouge.
- **Nombre de drones survivants :** nombre de drones encore en vie à la fin du combat.
- **Nombre de tourelles survivantes :** nombre de tourelles encore en vie à la fin du combat.
- **Points de vie cumulés restants :** total des points de vie des unités survivantes.
- **Durée de la bataille :** temps nécessaire pour terminer le combat.

Les expérimentations seront réalisées progressivement :

- **Version A — Référence :** comportement initial de l'armée rouge.
- **Version B — Optimisation tactique :** amélioration de la sélection des cibles et de la concentration des attaques.
- **Version C — Stratégie complète :** intégration des améliorations des comportements et de la fonctionnalité supplémentaire.

Les résultats permettront de déterminer quelles modifications apportent une amélioration réelle.

Les situations de blocage (*deadlock*) identifiées comme non comptabilisables par le sujet seront relancées conformément aux consignes.

## 10. Répartition du travail

La répartition proposée pour une équipe de quatre étudiants est la suivante :

| Membre | Responsabilités principales |
|---|---|
| Membre 1 | Gestionnaire de l'armée et stratégie globale |
| Membre 2 | Arbre de comportement et logique des drones |
| Membre 3 | Arbre de comportement et logique des tourelles |
| Membre 4 | Fonctionnalité supplémentaire, tests et analyse statistique |

L'intégration, la validation et le débogage devront être réalisés collectivement. La répartition définitive sera adaptée aux compétences et aux contributions réelles de chaque membre.

## 11. Résultats attendus

Le projet vise à obtenir :

- Une armée rouge capable de prendre des décisions tactiques plus cohérentes.
- Une meilleure coordination entre les unités.
- Une réduction des attaques inefficaces et des déplacements inutiles.
- Des arbres de comportement structurés et faciles à maintenir.
- Une fonctionnalité supplémentaire opérationnelle.
- Des résultats statistiques permettant d'évaluer objectivement les améliorations.

Ces résultats constituent les objectifs du projet et devront être confirmés par les tests.

## 12. Technologies utilisées

- **Unity 3D** : moteur de jeu.
- **C#** : langage de programmation.
- **Behavior Designer** : conception des arbres de comportement.
- **Git** : gestion du code source et collaboration.
- **CSV / tableur** : collecte et analyse des résultats de simulation.

## 13. Conclusion

Notre approche consiste à améliorer en priorité la prise de décision de l'armée rouge, notamment la sélection des cibles, la concentration des attaques et la coordination entre les drones et les tourelles.

Le développement sera guidé par une démarche expérimentale : établir une référence, modifier les comportements progressivement, mesurer les résultats et conserver les améliorations démontrées par les simulations.

L'objectif final est de proposer une IA tactiquement cohérente, techniquement robuste et plus efficace que l'IA verte par défaut, tout en respectant les contraintes du sujet.
