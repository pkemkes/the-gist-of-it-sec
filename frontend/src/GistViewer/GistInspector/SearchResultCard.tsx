import { Card, CardActionArea, CardContent, CardMedia, Typography } from "@mui/material"
import { SearchResult } from "../../types"

interface SearchResultCardProps {
  searchResult: SearchResult
}

export const SearchResultCard = ({ searchResult }: SearchResultCardProps) => {
  return <Card elevation={ 3 } sx={{ 
    mb: "1rem",
  }}>
    <CardActionArea
      component="a"
      href={ searchResult.url }
      target="_blank"
    >
      {
        searchResult.thumbnailUrl != undefined 
          ? <CardMedia
              component="img"
              sx={{
                height: "9rem",
                width: "30%",
                maxWidth: "15rem",
                objectFit: "cover",
                float: "right",
                ml: "1rem",
              }}
              image={ searchResult.thumbnailUrl }
            />
          : undefined
      }
      <CardContent>
        <Typography sx={{ fontSize: "1.3rem", mb: "0.5rem" }}>
          { searchResult.title }
        </Typography>
        <Typography variant="body2">
          { searchResult.snippet }
        </Typography>
        <Typography
          color="primary"
          sx={{
            fontSize: "0.9rem",
            mt: "1rem",
          }}
        >
          { searchResult.displayUrl.toLocaleUpperCase() }
        </Typography>
      </CardContent>
    </CardActionArea>
  </Card>
}