(() => {
  const annotations = {
    levels: [[14,5,'Розділ «Рівні»'],[32,20.3,'Пошук номера або ID'],[21,28,'Картка відкриває чернетку']],
    field: [[14,9,'Розділ «Поле»'],[32,16,'Розмір сітки'],[21,25,'Інструмент клітин']],
    objects: [[22,13.8,'Категорія моделей'],[20,29.5,'Оберіть модель'],[21,70.5,'Оберіть спосіб розміщення']],
    grass: [[61,13.8,'Категорія «Трава»'],[47,39.5,'Дрібна трава'],[21,61,'Пензель']],
    fence: [[43,16.5,'Категорія «Паркани»'],[20,30.5,'Секція тину'],[61,70.5,'Інструмент «Лінія»']],
    river: [[14,17,'Річка та земля'],[31,26.3,'Показати точки у сцені'],[21,36.5,'Додати точку русла']],
    terrain: [[21,42.5,'Додати пагорб'],[31,47.5,'Оберіть форму землі'],[31,57.5,'Змініть висоту']],
    guests: [[14,21,'Розділ «Гості»'],[31,16.7,'Вимога тіні'],[21,69.5,'Знайти еталонне рішення']],
    checks: [[14,25,'Розділ «Перевірка»'],[21,21,'Перевірити чернетку'],[21,40,'Застосувати до кампанії']]
  };
  document.querySelectorAll('figure img[data-zoom]').forEach(image => {
    const name = image.getAttribute('src').split('/').pop().replace('.png','');
    if (!annotations[name]) return;
    const figure = image.closest('figure'), wrap = document.createElement('div'), key = document.createElement('ol');
    wrap.className = 'image-wrap'; key.className = 'image-key';
    figure.insertBefore(wrap, image); wrap.appendChild(image);
    annotations[name].forEach(([x,y,label], index) => {
      const pin = document.createElement('span'); pin.className = 'image-pin'; pin.textContent = index + 1;
      pin.style.left = x + '%'; pin.style.top = y + '%'; pin.setAttribute('aria-hidden','true'); wrap.appendChild(pin);
      const item = document.createElement('li'); item.textContent = label; key.appendChild(item);
    });
    figure.querySelector('figcaption').appendChild(key);
  });
  const search = document.querySelector('#search');
  const sections = [...document.querySelectorAll('[data-search]')];
  const links = [...document.querySelectorAll('nav a')];
  const status = document.querySelector('#search-result');
  search.addEventListener('input', () => {
    const query = search.value.trim().toLocaleLowerCase('uk');
    let count = 0;
    sections.forEach(section => {
      const match = !query || section.textContent.toLocaleLowerCase('uk').includes(query);
      section.hidden = !match;
      if (match) count++;
      const link = links.find(a => a.hash === '#' + section.id);
      if (link) link.hidden = !match;
    });
    status.hidden = !query;
    status.textContent = count ? `Знайдено розділів: ${count}.` : 'Нічого не знайдено. Спробуйте «річка», «поле» або «зберегти».';
  });
  links.forEach(link => link.addEventListener('click', () => {
    links.forEach(a => a.classList.toggle('active', a === link));
  }));
  const dialog = document.querySelector('#zoom');
  document.querySelectorAll('img[data-zoom]').forEach(image => {
    image.tabIndex = 0;
    const open = () => {
      dialog.querySelector('img').src = image.src;
      dialog.querySelector('img').alt = image.alt;
      dialog.querySelector('p').textContent = image.closest('figure').querySelector('figcaption').textContent;
      dialog.showModal();
    };
    image.addEventListener('click', open);
    image.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); open(); } });
  });
  dialog.querySelector('.close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
  document.querySelector('#print').addEventListener('click', () => window.print());
})();
