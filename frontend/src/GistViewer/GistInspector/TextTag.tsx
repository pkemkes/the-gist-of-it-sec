import { Typography } from "@mui/material"

interface TextTagProps {
  tagText: string
}

export const TextTag = ({ tagText }: TextTagProps) => {
  return <Typography
    sx={{
        mr: "0.9rem",
        mb: "0.5rem",
        fontSize: "0.7rem",
        WhiteSpace: "nowrap",
    }}
  >
    { 
      tagText.toLocaleUpperCase()
    }
  </Typography>;
}