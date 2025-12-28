import { backendApi } from "../../backend";
import { GistCard } from "../GistCard";
import { GistEndCard } from "../GistEndCard";
import { LoadingBar } from "../LoadingBar";
import { ErrorMessage } from "../ErrorMessage";
import { useAppSelector } from "../../store";
import { selectDisabledFeeds, selectLanguageMode } from "../slice";

interface SimilarGistListProps {
  gistId: number
}

export const SimilarGistList = ({ gistId }: SimilarGistListProps) => {
  const disabledFeeds = useAppSelector(selectDisabledFeeds);
  const languageMode = useAppSelector(selectLanguageMode);
  const { data, error, isFetching } = backendApi.useGetSimilarGistsQuery({ id: gistId, disabledFeeds, languageMode });

  if (error || data == undefined && !isFetching) {
    return <ErrorMessage />;
  }

  const dataToDisplay = data ? data.map(({ gist, similarity }, i) => (
    <GistCard key={ i } gist={ gist } similarity={ similarity } />
  )) : [];

  dataToDisplay.push(isFetching 
    ? <LoadingBar key={dataToDisplay.length} /> 
    : <GistEndCard key={dataToDisplay.length} />
  );

  return dataToDisplay
}