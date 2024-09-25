import { IconButton, Tooltip } from "@mui/material";
import TelegramIcon from '@mui/icons-material/Telegram';

export const TelegramButton = () => {
	const telegramUrl = import.meta.env.VITE_TELEGRAM_URL;
  if (!telegramUrl) {
    return undefined;
  }

  return <Tooltip title="Open Telegram bot" placement="top-start">
    <IconButton
      component="a"
      href={ telegramUrl }
      target="_blank"
      sx={{ ml: "0.5rem" }}
      size="small"
    >
      <TelegramIcon />
    </IconButton>
  </Tooltip>
}