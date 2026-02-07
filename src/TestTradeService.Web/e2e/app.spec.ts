import { expect, test } from '@playwright/test';

test('главная страница открывается и содержит русское меню', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByText('Настройка источников и тикеров')).toBeVisible();
  await expect(page.getByText('Поток данных')).toBeVisible();
  await expect(page.getByText('Обработанные данные')).toBeVisible();
  await expect(page.getByText('Мониторинг и алертинг')).toBeVisible();
});

