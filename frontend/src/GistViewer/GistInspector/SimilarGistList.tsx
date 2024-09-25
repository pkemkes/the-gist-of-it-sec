import { backendApi } from "../../services/backend";
import { GistCard } from "../GistCard";
import { GistEndCard } from "../GistEndCard";
import { LoadingBar } from "../LoadingBar";
import { ErrorMessage } from "../ErrorMessage";

interface SimilarGistListProps {
  gistId: number
}

export const SimilarGistList = ({ gistId }: SimilarGistListProps) => {
  const { data, error, isFetching } = backendApi.useGetSimilarGistsQuery({ id: gistId });

  if (error || data == undefined && isFetching) {
    return <ErrorMessage />;
  }

  const dataToDisplay = data ? data.map(({ gist, similarity }, i) => (
    <GistCard key={ i } gist={ gist } similarity={ similarity } />
  )) : [];

  dataToDisplay.push(isFetching ? <LoadingBar /> : <GistEndCard />);

  return dataToDisplay
}