# Identity Crisis — RimWorld

> Захватите планету. Перепишите тела. Оставьте шрамы.

Мод для RimWorld 1.6 об экспансии инопланетной цивилизации. Вы начинаете горсткой чистокровных пришельцев, захватываете местных жителей, ломаете их волю или ассимилируете силой и постепенно заселяете планету своей расой. Обращённые сохраняют память и навыки, но вместе с ними — травмы, и навсегда остаются низшим слоем общества.

## Возможности

- **Капсула ассимиляции** — исследуемое здание, которое за сутки перестраивает тело пешки.
- **Окно настройки** — раса (любая человекоподобная раса из модов на Humanoid Alien Races), пол, возраст, тип тела и головы, причёска, борода и цвет волос.
- **Прогноз травм** — прямо в окне видно, будет ли обращение добровольным или насильным и какие моральные травмы получит пешка.
- **Личность сохраняется** — имя, навыки, страсти, опыт, черты, отношения, фракция, инвентарь и импланты (если у новой расы есть подходящая часть тела).
- **Запись в биографии** — «Биологический неофит» или «Переживший стирку формы».
- **Психология обращённых** — у добровольных фантомный дискомфорт со временем проходит, у насильных тяжёлый шок переходит в пожизненную травму с риском срывов. Смена пола добавляет отдельную травму.
- **Чистокровные** считают себя высшим видом и смотрят на обращённых свысока.

## Требования

- RimWorld 1.6
- [Humanoid Alien Races](https://steamcommunity.com/sharedfiles/filedetails/?id=839005762) и хотя бы одна раса на его основе
- Ideology — по желанию (рабы, идеология, подавление)
- Biotech — по желанию (гены)

## Статус

🚧 Ранняя разработка (v0.1). Код ещё не собирался и не проверялся в игре.

## Сборка

```
cd Source/IdentityCrisis
dotnet build -c Release
```

DLL появится в `1.6/Assemblies/`. Затем поместите папку мода в `RimWorld/Mods/`.

## English

Identity Crisis is a RimWorld 1.6 mod about alien expansion. Capture the locals and rebuild them in the Assimilation Pod into any humanlike race from Humanoid Alien Races. Pick the race, gender, age and appearance in the setup window and see the predicted psychological consequences before you commit. The body changes completely; the mind stays. Every convert gets a permanent biography mark. Voluntary converts slowly adapt, forced converts never fully recover, and purebloods see themselves as the superior kind. Requires Humanoid Alien Races; Ideology and Biotech are optional. Early development, untested.
