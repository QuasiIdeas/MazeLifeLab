  + - - - + - + - -
  + - + - + copyright by Vladimir Baranov (Kvazikot)  <br>
  + - + - + email: vsbaranov83@gmail.com  <br>
  + - + - + github: http://github.com/Kvazikot/BallPark  <br>
```
                            )            (
                           /(   (\___/)  )\
                          ( #)  \ ('')| ( #
                           ||___c\  > '__||
                           ||**** ),_/ **'|
                     .__   |'* ___| |___*'|
                      \_\  |' (    ~   ,)'|
                       ((  |' /(.  '  .)\ |
                        \\_|_/ <_ _____> \______________
                         /   '-, \   / ,-'      ______  \
                b'ger   /      (//   \\)     __/     /   \
                                            './_____/
```              
  

# Пути исследования научного космоса.
* Можно реализовать что-то наподобии моделирования среды хищник-жертва см [1].
* Можно продолжить возиться с паркинг-слотами и прокладыванием траектории и обучать агентов парковаться.
* 

## Библиотеки машинного обучения
1.	Среда ml-agents
Это специальная среда для обучения ml-агентов. Ее можно применить для соединения Пайтона со средой Unity. При этом обеспечивается контроль обучения в TensorBoard.
https://github.com/Unity-Technologies/ml-agents


# Лабиринт в 3д (unity-проект roll a ball).
[![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/ballpark01.png)](https://youtu.be/YBzOM5-RKNE)
[![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/ballpark2.png)](https://youtu.be/2Rz0iVN7zgA)

# Модель движения машинки(unity-проект car_model).
Отдельный проект на Юнити car_model
Здесь создана динамическая модель авто, используя инструменты Unity по туториалу, который лежит в видео файлах. Управление в режиме игры пока с клавиатуры.
  
 Алгоритм планирования пути RRT.
(Входит в состав Unity-проекта car_model.)

Алгоритмическая задача: исследовать алгоритм RRT для управления чтобы обходить коллизии и проходить маршрут от начальной точки, через промежуточные и до конечной
Алгоритм Rapidly-exploring Random Trees. (см. статью PlanningforDynamicVeh-1.pdf)

# Програмный модуль rrt_planer.cs
На первом этапе можно реализовать алгоритм простого дерева без интегрирования траектории по Рунге-Кутта (как описано в LavKuf01.pdf).  Figure 2.

![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/rrt_with_integration.png)

В качестве множества препятствий можно использовать что-то похожее на этот рисунок.
В юнити это означает что нужно вместо линий нарисовать протяженные объекты типа параллепипедов.
Переход к лабиринту осуществится на поздних итерациях проекта.
На первых этапах сосредоточится на построении графа и отрисовке траектории в виде линий и не думать пока о объекте управления. Потом подключить кинематическую модель машины для расчета поворотов. Построить траектории в виде кривых. Когда будет работать алгоритм планирования пути можно будет подключать скрипты управления машиной . В качестве входа взять граф управления построенный по алгоритму RRT (rrt_planer.cs)
 

# Вопросы по алгоритмической части:
Как выбирать распределение в функции random_config.(подход goal biased или uniform)?
В функции new_state что выбирать за u скорость или угол поворота колес, и то и другое?
Из какого множества выбирать скорости или углы при каждом вызове new_state?
По какому критерию выбирать одну конкретную траекторию из множества всех возможных в графе?


# Интеграция c++ кода в Unity
Поэтому на первом этапе нужно:
1.	создать плагин на с++ RRTPlanerPlugin,
2.	положить его в каталог Plugins проекта car_model.  Как описано в этом туториале.
3.	Посмотреть как работать с базовыми типа Unity такими как Vector3.
4.	Продумать как создавать обертки вокруг нативных классов. См. каталог wrapper_class
a.	(Здесь важно какой код создает и уничтожает обьект, управляемый или не управляемый)

# Преимущества нативного кода.
   Более высокая скорость выполнения. Пайтон и c# в этом смысле не очень

В каталоге Unity Projects\car_model\Assets\Plugins содержатся функции экспорта типов c# в c++ плагин.


## Тест распределения.

1.	Подключить TestDll к скрипту RttPlaner.cs и сделать один делегат GenerateCoordinates
2.	Подготовить тест в RttPlaner.cs тест будет отправлять в TestDll параметры распределения максимальные и минимальные значения для X и Z компоненты. На выходе c++ код сгенерирует два массива типа double с координатами
3.	Чтобы проверить правильность генерации координат можно создать сферы точечного размера в координатах из этих двух массивов


# Что мне не нравится в этой картинке?
 
Во первых траектория меняется слишком резко. Это предположительно изза того что функция steering имеет случайный характер (см. следующий заголовок)
Bторое есть дефекты в функции детектора коллизий.
Нам нужна функция сглаженного галсового шума с кубической сплайновой интерполяцией.

# Следующий этап. Плитки из miniRRT. Обучение CNN на большом количестве лабиринтов. 
Этот этап будет в пайтоне, нужно будет переписать код лабиринта и rrt.
За основу можно взять программу [snake-ai-pytorch-main](https://github.com/python-engineer/snake-ai-pytorch)
Можно сделать обучение агента наподобии CubeAgent проекта BallPark только с управлением моделью авто.
6 агнетов в 6 секциях как на рисунке. 
Возможно работать на какой-то регулярной сетке c LOD типа [вот этой](https://github.com/Kvazikot/BunchofBaranovIdeas/blob/master/DiamondPlates.MD)

![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/agents_learning_env.png)

Можно рабить все изображение лабиринта на большое количество мелких сегментов (квадратных элементов мозайки) 
Потом обучить нейросеть составлять RRT имея только изображение лабиринта (зеленые стены).
Пои идее это ускорнит алгоритм, поскольку не надо считать пересечения.
Можно пойти дальше и составить изображения всевозможных маневров, например для парковки.

Скриншоты 19.11.2020
![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/parking_slots.png)
![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/path_planer.png)
![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/trajectory.png)
![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/waypoints.png)

новый генератор обучающей среды для агента типа автомобиль, должно быть таких 6 сред.
доработанный планировщик пути (заменен Raycast на Spherecast) и изменено распределение в ддлке. Путь можно строить в реальном времени, выбирать оптимальную траекторию пощаще.

## Post statistical control
Идея постатистики состоит в том что сначала мы формируем некую статистику по траектории. После чего даем награду агенту если его движения соответсвует этой статистике т.е. дисперсия и мат отклонение.

это видео из ютьюба иллюстрирует данный концепт
[![image](https://github.com/Kvazikot/BallPark/blob/main/screenshots/poststatistical.png)](https://www.youtube.com/embed/b8YtXNklqpM)

## Литература:

1. Nonlinear Dynamics And Chaos With Applications To Physics, Biology, Chemistry, And Engineering by Steven H. Strogatz
2. Rapidly-Exploring Random Trees: Progress and Prospects
3. [Unity Machine Learning Agents](https://unity.com/products/machine-learning-agents)
4. Turgut, A. E., Çelikkanat, H., Gökçe, F., & Şahin, E. (2008). Self-organized flocking in mobile robot swarms. Swarm Intelligence, 2, 97–120.
5. Trianni, V., & Dorigo, M. (2006). Self-organisation and communication in groups of simulated and physical robots. Biological Cybernetics, 95, 213–231.
6. Baldassarre, G., Trianni, V., Bonani, M., Mondada, F., Dorigo, M., & Nolfi, S. (2007). Self-organized coordinated motion in groups of physically connected robots
